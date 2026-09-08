#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.Vehicles;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// ⭐⭐ <b>THE JOURNEY THE PLAYER ACTUALLY TAKES</b> — a driver reverses a tractor onto a loose
    /// trailer's kingpin, gets out, and works the handle.
    ///
    /// <para><b>Why this file exists.</b> The owner, in play at the laydown 2026-09-08:
    /// <i>"i cannot get trailers to couple."</i> Every coupling test in the repo already passed, and
    /// every one of them had placed the pin in the slot by arithmetic —
    /// <c>TrailerCouplingJourneyPlayTests</c> teleports the plate onto the pin with
    /// <c>PlacePlateAt</c> and then couples what it put there. <b>No test had ever REVERSED a tractor
    /// into a kingpin.</b> The player was the first to try it, and he could not do it. A window
    /// nothing ever drove through is a window nobody had measured.</para>
    ///
    /// <para><b>What it measures, and reports either way.</b> Three starting offsets — square on, and
    /// 30 cm either side — driven straight back with <b>no steering correction at all</b>, so the
    /// offset means what it says: the driver did not save it, the funnel had to swallow it. For each
    /// run it records how close the pin came to the seat and, at that same instant, <b>the heading
    /// delta against the capture gate</b> — because #784 widened the LATERAL half of this window and
    /// left the 8.53° heading half untouched, and nobody has measured whether that half is a blocker
    /// too. If an offset clears the throat and fails on heading, that is the finding; it is reported,
    /// not fixed here.</para>
    ///
    /// <para>⚠️ <b>Nothing is stepped by frame count.</b> A batch-mode frame is ~0.4 ms, so a fixed
    /// number of them buys an amount of driving that depends on the machine. Every loop below runs to
    /// a CONDITION — captured, or driven past her — and carries an explicit anti-vacuous assertion
    /// that she really travelled, because "not captured" and "never moved" look identical in a
    /// summary.</para>
    ///
    /// <para>⚠️ No key is pressed and no save is written: the truck is driven through
    /// <see cref="VehicleController.Throttle"/>, and the press goes through the production
    /// <see cref="InteractVerb.TryPerform"/> with a hand-built actor.</para>
    /// </summary>
    public class ReverseIntoThePinPlayTests
    {
        /// <summary>Bay 5's occupant, by the builder's own name for her — the 53-ft reefer.</summary>
        const string BayFiveReefer = "Reefer53AtTheLaydown";

        /// <summary>How far ahead of the seat he starts, metres. The charter's number: far enough that
        /// the run is a manoeuvre rather than a nudge.</summary>
        const float RunUpMetres = 12f;

        readonly List<Object> _spawned = new();

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            Interactables.Clear();
            // ⚠️ A fixture that boots into the shell gets the TITLE PAGE, where WorldInputBlocked is
            // true and every interact press silently does nothing — the press would return false and
            // read as "the handle refused". Reset() is the slate wipe; StartNewGame() is the one that
            // would write the owner's save, and is deliberately not called.
            if (ShellFlow.WorldInputBlocked) ShellFlow.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            Interactables.Clear();
            GameServices.Reset();
            foreach (var o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();
        }

        // ---- the yard, as built ------------------------------------------------------------------

        sealed class Rig
        {
            public GameObject TractorGo, TrailerGo;
            public VehicleController Controller;
            public VehicleHitch Hitch;
            public TowedBody Trailer;
            public VehicleDoors Gear;
            public float YardHeading;
        }

        /// <summary>Bay 5's reefer where the laydown actually stands her, and a tractor
        /// <see cref="RunUpMetres"/> ahead of the seat on the yard heading, pushed
        /// <paramref name="lateralOffset"/> off the centreline.</summary>
        Rig Build(float lateralOffset)
        {
            NineMileCreekLaydown.Placement bay5 = default;
            bool found = false;
            foreach (NineMileCreekLaydown.Placement p in NineMileCreekLaydown.Solve())
                if (p.Unit.Name == BayFiveReefer) { bay5 = p; found = true; break; }

            Assert.IsTrue(found,
                $"the laydown solved no '{BayFiveReefer}' — the yard is unbuilt or a bay was renamed, " +
                "and every measurement below would be about a trailer of this test's own invention.");
            Assert.IsTrue(bay5.Mesh != null && bay5.Mesh.IsTowable,
                "bay 5's reefer publishes no kingpin — re-run the vehicle bake.");

            VehicleMeshDef tractorMesh = null;
            foreach (NineMileCreekLaydown.Placement p in NineMileCreekLaydown.Solve())
                if (p.Mesh != null && p.Mesh.CanTow) { tractorMesh = p.Mesh; break; }
            Assert.IsNotNull(tractorMesh, "the yard holds no machine with a fifth wheel.");

            // ---- the trailer, exactly where the builder puts her, legs DOWN as she bakes parked
            var trailerGo = new GameObject("BayFiveReefer");
            _spawned.Add(trailerGo);
            trailerGo.transform.position = new Vector3(bay5.Position.x, bay5.Position.y, 0f);
            trailerGo.transform.rotation = bay5.Rotation;

            var gear = trailerGo.AddComponent<VehicleDoors>();
            gear.Configure(bay5.Mesh);
            gear.SnapAllShut();

            var body = trailerGo.AddComponent<TowedBody>();
            body.Configure(bay5.Mesh);
            body.HeadingDegrees = bay5.HeadingDegrees;

            // ---- the tractor
            var tractorGo = new GameObject("Tractor", typeof(Rigidbody2D));
            _spawned.Add(tractorGo);
            var controller = tractorGo.AddComponent<VehicleController>();
            controller.SetVehicle(Drivable(tractorMesh));
            var hitch = tractorGo.AddComponent<VehicleHitch>();
            hitch.Configure(tractorMesh, controller, "vehicle.reversing_tractor");

            var rig = new Rig
            {
                TractorGo = tractorGo, TrailerGo = trailerGo, Controller = controller,
                Hitch = hitch, Trailer = body, Gear = gear, YardHeading = bay5.HeadingDegrees,
            };

            // Stand her on the yard heading with her plate RunUpMetres ahead of the pin, and shoved
            // sideways by the offset under test. Ahead = along her own nose, which is where a tractor
            // sits before she backs under a trailer.
            Vector2 ahead = Heading(bay5.HeadingDegrees);
            Vector2 across = new Vector2(ahead.y, -ahead.x);
            PlacePlateAt(rig, body.KingpinWorld + ahead * RunUpMetres + across * lateralOffset,
                         bay5.HeadingDegrees);
            return rig;
        }

        VehicleDef Drivable(VehicleMeshDef mesh)
        {
            var def = ScriptableObject.CreateInstance<VehicleDef>();
            _spawned.Add(def);
            def.Id = "vehicle.reversing_tractor";
            def.Mesh = mesh;
            def.MaxSpeedMetersPerSecond = 8f;
            def.AccelerationMetersPerSecondSquared = 6f;
            def.BrakingMetersPerSecondSquared = 8f;
            def.SteerRateFullLocksPerSecond = 2f;
            def.CameraWorldHeightMeters = 22f;
            return def;
        }

        /// <summary>The unit vector a bearing points along — the same convention
        /// <c>BoatKinematics.BearingDegrees(transform.up)</c> reads back.</summary>
        static Vector2 Heading(float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(r), Mathf.Cos(r));
        }

        /// <summary>Put the tractor's PLATE at a world point on a heading, stilled — a Rigidbody2D
        /// carries velocity across a teleport, and a truck that arrives already rolling would have
        /// started her run before the test began measuring it.</summary>
        static void PlacePlateAt(Rig r, Vector2 plateWorld, float headingDegrees)
        {
            var rb = r.TractorGo.GetComponent<Rigidbody2D>();
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;

            r.TractorGo.transform.rotation = Quaternion.Euler(0f, 0f, -headingDegrees);
            r.TractorGo.transform.position = Vector3.zero;
            Vector2 plateAtOrigin = r.Hitch.CouplingPointWorld;
            Vector2 shift = plateWorld - plateAtOrigin;
            r.TractorGo.transform.position = new Vector3(shift.x, shift.y, 0f);
        }

        // =============================================================================================
        //  THE RUN
        // =============================================================================================

        [UnityTest] public IEnumerator HeBacksOntoThePin_SquareOn() => Reverse(0f);
        [UnityTest] public IEnumerator HeBacksOntoThePin_ThirtyCentimetresOffOneSide() => Reverse(+0.3f);
        [UnityTest] public IEnumerator HeBacksOntoThePin_ThirtyCentimetresOffTheOther() => Reverse(-0.3f);

        /// <summary>
        /// Reverse until the pin is taken, then do the rest of the job on foot.
        ///
        /// <para><b>No steering.</b> A test that corrected its way in would be measuring its own
        /// controller, not the window: the offsets are the point, and they have to survive unaided.</para>
        /// </summary>
        IEnumerator Reverse(float lateralOffset)
        {
            Rig r = Build(lateralOffset);
            string who = $"backing in {lateralOffset:+0.00;-0.00;0.00} m off the centreline";

            VehicleFifthWheel wheel = r.Hitch.FifthWheel;
            float gate = VehicleCouplingMath.CaptureHeadingToleranceDegrees(wheel);
            float startOdo = r.Controller.OdometerMeters;

            float closest = float.MaxValue, headingAtClosest = float.NaN, lateralAtClosest = float.NaN;
            TowedBody captured = null;

            // ⚠️ A CONDITION, not a frame budget: run until she takes the pin or has reversed clean
            // past the seat. The distance cap is her own odometer, so a fast box and a slow one drive
            // exactly the same journey.
            r.Controller.Throttle = -0.3f;
            r.Controller.SteerDemand = 0f;
            r.Controller.Brake = false;

            while (Mathf.Abs(r.Controller.OdometerMeters - startOdo) < RunUpMetres + 6f)
            {
                yield return new WaitForFixedUpdate();
                yield return null;

                Vector2 pin = r.Trailer.KingpinWorld;
                Vector3 local = r.TractorGo.transform.InverseTransformPoint(new Vector3(pin.x, pin.y, 0f));
                float toSeat = Vector2.Distance(new Vector2(local.x, local.y),
                                                new Vector2(wheel.CouplingPointLocal.x, wheel.CouplingPointLocal.y));
                if (toSeat < closest)
                {
                    closest = toSeat;
                    lateralAtClosest = local.x - wheel.CouplingPointLocal.x;
                    headingAtClosest = Mathf.DeltaAngle(r.Hitch.HeadingDegrees, r.Trailer.HeadingDegrees);
                }

                captured = r.Hitch.CapturedTrailer();
                if (captured != null) break;
            }
            r.Controller.Throttle = 0f;

            float travelled = Mathf.Abs(r.Controller.OdometerMeters - startOdo);
            string report =
                $"{who}: travelled {travelled:0.00} m; closest the pin came to the seat {closest:0.000} m " +
                $"(lateral {lateralAtClosest:0.000} m of the funnel's {wheel.ThroatHalfWidthMeters:0.000} m " +
                $"throat / {wheel.SlotHalfWidthMeters:0.000} m jaw); heading Δ at that instant " +
                $"{headingAtClosest:0.000}° of the {gate:0.000}° gate.";
            Debug.Log("[reverse-into-the-pin] " + report);

            // ⭐ THE ANTI-VACUOUS ARM. "Not captured" and "never moved" are the same line in a summary,
            // and only one of them is a finding about the window.
            Assert.That(travelled, Is.GreaterThan(RunUpMetres * 0.5f),
                $"{who}: she only travelled {travelled:0.00} m of a {RunUpMetres:0} m run-up — this test " +
                "did not measure the capture window, it measured a truck that would not go. " + report);

            Assert.IsNotNull(captured,
                $"{who}: reversed the whole way onto her and never took the pin. {report}\n" +
                (Mathf.Abs(headingAtClosest) > gate
                    ? "⚠️ THE HEADING GATE IS WHAT REFUSED HER — the pin was inside the funnel and the "
                      + "approach angle was not. #784 widened the lateral half of this window and left "
                      + "this half at the jaw's own aspect on purpose; this is the measurement that says "
                      + "whether that was right. Report it — do NOT widen the aspect to make this pass."
                    : "the pin never reached the funnel at all — this is the lateral half, and it is the "
                      + "one #784 was supposed to have fixed."));

            Assert.AreSame(r.Trailer, captured, $"{who}: something other than bay 5's reefer was taken.");

            // ---- and now the half he could not do from the seat -------------------------------------
            yield return OnFootFromHereToCoupled(r, who);

            // ---- pull away, and she follows ---------------------------------------------------------
            Vector2 trailerBefore = r.TrailerGo.transform.position;
            float pullOdo = r.Controller.OdometerMeters;
            r.Controller.Throttle = 1f;
            while (Mathf.Abs(r.Controller.OdometerMeters - pullOdo) < 20f)
            {
                yield return new WaitForFixedUpdate();
                yield return null;
            }
            r.Controller.Throttle = 0f;

            float trailerMoved = Vector2.Distance(trailerBefore, r.TrailerGo.transform.position);
            Assert.That(trailerMoved, Is.GreaterThan(15f),
                $"{who}: he pulled away 20 m and she moved {trailerMoved:0.0} m — she is on the pin and " +
                "not following.");
            Assert.IsTrue(r.Trailer.IsCoupled, $"{who}: she came off the pin under tow.");
        }

        /// <summary>
        /// ⭐ <b>Out of the cab, to the handle, one press.</b>
        ///
        /// <para>The actor stands where the release handle is and presses the ONE interact verb —
        /// <see cref="InteractVerb.TryPerform"/>, the production path, no key and no second verb of its
        /// own. This is the step that had no feedback behind it before #787: from the seat, a captured
        /// pin and a missed one looked identical, so the driver walked out here to find out.</para>
        /// </summary>
        IEnumerator OnFootFromHereToCoupled(Rig r, string who)
        {
            Assert.IsTrue(r.Hitch.IsAvailable,
                $"{who}: the pin is in the slot and the handle offers nothing to the man walking up to it.");
            Assert.AreEqual("Couple the trailer", r.Hitch.VerbLabel,
                $"{who}: the handle reads as a release with nothing yet on the plate.");

            var actor = new InteractActor(r.Hitch.WorldPosition, Vector2.zero, InteractContext.OnFoot);
            Assert.IsTrue(InteractVerb.TryPerform(actor, 180f),
                $"{who}: standing at the release handle, the one interact verb resolved nothing.");
            yield return null;

            Assert.IsTrue(r.Hitch.IsCoupled, $"{who}: the press landed and she is not on the pin.");
            Assert.IsTrue(r.Trailer.IsCoupled, $"{who}: coupled, but she does not know it.");
            Assert.AreEqual("Pull the release", r.Hitch.VerbLabel,
                $"{who}: on the plate, and the handle still offers to couple.");

            // ⚠️ WINDING, not wound: the crank takes its published time, which is the honest picture
            // the kit asks for. Her shoes must have left the ground and must not have arrived.
            Assert.That(r.Gear.Openness("LandingGearShoes"), Is.LessThan(1f),
                $"{who}: her shoes teleported to fully raised the instant the pin dropped.");
            r.Gear.Advance(0.05f);
            yield return null;
            Assert.IsFalse(r.Trailer.LegsAreDown,
                $"{who}: her shoes are lifting and she still reads as standing on them.");

            float t = 0f;
            while (t < 3.0f) { r.Gear.Advance(0.05f); t += 0.05f; yield return null; }   // past the published 2.4 s
            Assert.IsFalse(r.Trailer.LegsAreDown,
                $"{who}: the crank ran its full time and her legs are still on the ground.");
        }
    }
}
#endif
