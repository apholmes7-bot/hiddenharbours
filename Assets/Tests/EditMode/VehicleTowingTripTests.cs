using System.Linq;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐⭐ <b>A SCHEDULED TRIP THAT HAULS SOMETHING</b> — road fleet PR 6a.
    ///
    /// <para>Two subjects, and the second one is the interesting half.</para>
    ///
    /// <para><b>1. The ten-block day.</b> A towing run grows two beats — the driver walks to the
    /// street-side release, the pin goes in, the legs come up; and the reverse at dusk — and it is still
    /// only TWO authored hours, because a beat's length is the walk it contains. The trailer's own pose
    /// comes off <c>TowedFollowTrack</c> (see <c>TowedFollowTrackTests</c> for the proof that it is the
    /// player's own tow).</para>
    ///
    /// <para><b>2. WHERE A TOWING TRIP CAN RUN AT ALL, which is a much narrower place than a solo one.</b>
    /// A posed body cannot reverse; a coupled pair cannot pivot; and a trailer is re-derived from the
    /// clock rather than saved, so the day has to CLOSE — she must end it exactly on the plate she was
    /// picked up off, or she teleports at midnight. Put together those force both ends of the road to be
    /// PULL-THROUGHS: she leaves each bay on the heading she arrived on. An ordinary there-and-back on
    /// one road can never satisfy that, and this fixture says so out loud, with the miss measured —
    /// because that refusal is a real constraint on the world, not a gap in the code.</para>
    /// </summary>
    public class VehicleTowingTripTests
    {
        const string Aero = "Assets/_Project/Data/Vehicles/Meshes/AeroSemiVehicleMesh.asset";
        const string Classic = "Assets/_Project/Data/Vehicles/Meshes/ClassicSemiVehicleMesh.asset";
        const string Box = "Assets/_Project/Data/Vehicles/Meshes/CaboverBoxVehicleMesh.asset";

        static readonly string[] Trailers =
        {
            "Assets/_Project/Data/Vehicles/Meshes/TrailerReefer28VehicleMesh.asset",
            "Assets/_Project/Data/Vehicles/Meshes/TrailerFlatbed28VehicleMesh.asset",
            "Assets/_Project/Data/Vehicles/Meshes/TrailerReefer53VehicleMesh.asset",
            "Assets/_Project/Data/Vehicles/Meshes/TrailerFlatbed53VehicleMesh.asset",
        };

        const float SecondsPerGameHour = 60f;   // a 24-minute day, the config's own shape
        const float Cruise = 7f, Walk = 1.4f, Out = 6f, Home = 15f;

        static VehicleMeshDef Load(string path)
        {
            var def = AssetDatabase.LoadAssetAtPath<VehicleMeshDef>(path);
            Assert.That(def, Is.Not.Null, $"{path} did not load — re-run the vehicle bake.");
            return def;
        }

        // =============================================================================================
        //  A ROAD A TOWING TRIP CAN ACTUALLY RUN ON
        // =============================================================================================

        /// <summary>Out of the bay heading north, round two bends to the far bay heading east.</summary>
        static Vector2[] Outbound() => new[]
        {
            new Vector2(0f, 0f), new Vector2(0f, 60f), new Vector2(20f, 80f), new Vector2(60f, 80f),
        };

        /// <summary>⭐ Home the LONG way — out of the far bay still heading east, round the circuit, and
        /// back UP into the home bay heading north. Both bays are entered on the heading they are left
        /// on, which is what a pull-through means and what a coupled pair requires.</summary>
        static Vector2[] Return() => new[]
        {
            new Vector2(60f, 80f), new Vector2(100f, 80f), new Vector2(120f, 40f),
            new Vector2(120f, -40f), new Vector2(60f, -60f), new Vector2(0f, -60f),
            new Vector2(0f, -20f), new Vector2(0f, 0f),
        };

        /// <summary>The same errand on one road: out and back the way she came. Fine for a solo truck,
        /// impossible with a trailer on the pin.</summary>
        static Vector2[] ThereAndBack() => Outbound().Reverse().ToArray();

        /// <summary>Where a trailer stands to be picked up by a machine parked at <paramref name="at"/> on
        /// <paramref name="heading"/> — her pin in the MIDDLE of that machine's capture window, which is
        /// how <c>NineMileCreekLaydown</c> stands its own couple-ready pair. Nothing is typed: the window
        /// is the art's, and the step back from the pin to her origin is the coupling's one rotation.</summary>
        static Vector2 CoupleReady(VehicleMeshDef tractor, VehicleMeshDef trailer, Vector2 at,
                                   float heading)
        {
            VehicleFifthWheel wheel = tractor.FifthWheel;
            var local = new Vector2(wheel.CouplingPointLocal.x,
                                    (wheel.RampMouthY + wheel.SlotSeatY) * 0.5f);
            Vector2 pin = at + VehicleCouplingMath.LocalOffsetToWorld(local, heading);
            return VehicleCouplingMath.BodyOriginFromKingpin(pin, heading, trailer.Kingpin);
        }

        static VehicleTripSpec Spec(VehicleMeshDef tractor, VehicleMeshDef trailer, Vector2[] outbound,
                                    Vector2[] home, Vector2? trailerBay = null,
                                    float trailerHeading = 0f)
        {
            Vector2 bay = trailerBay ?? CoupleReady(tractor, trailer, outbound[0], trailerHeading);
            var towed = new VehicleTowedSpec(tractor.FifthWheel, trailer.Kingpin, bay, trailerHeading);

            return new VehicleTripSpec(outbound, home, new Vector2(-6f, -4f), Vector2.right,
                                       new Vector2(66f, 86f), Vector2.left, tractor.DriveDoorLocal,
                                       Out, Home, Cruise, Walk, towed);
        }

        static VehicleTripPlan BuiltPlan(out string problem, VehicleMeshDef tractor = null,
                                         VehicleMeshDef trailer = null)
        {
            tractor ??= Load(Aero);
            trailer ??= Load(Trailers[0]);
            return VehicleTripPlan.Build(Spec(tractor, trailer, Outbound(), Return()),
                                         SecondsPerGameHour, out problem);
        }

        // =============================================================================================
        //  1. THE TEN-BLOCK DAY
        // =============================================================================================

        [Test]
        public void ATowingTripRunsTenBlocks_AndOnlyTwoHoursAreStillTheOwners()
        {
            VehicleTripPlan plan = BuiltPlan(out string problem);
            Assert.That(plan, Is.Not.Null, problem);

            Assert.That(plan.BlockCount, Is.EqualTo(VehicleTripPlan.TowedLegCount));
            Assert.That(plan.Tows, Is.True);
            Assert.That(plan.Stages[VehicleTripPlan.TowedLegCouple],
                        Is.EqualTo(VehicleTripStage.Coupling));
            Assert.That(plan.Stages[VehicleTripPlan.TowedLegUncouple],
                        Is.EqualTo(VehicleTripStage.Uncoupling));

            // The two the owner types, and eight the geometry makes.
            Assert.That(plan.DepartureHours[VehicleTripPlan.TowedLegCouple], Is.EqualTo(Out).Within(1e-4f),
                "the authored departure is the hour her driver sets off — on a towing run that is his " +
                "walk to the HANDLE, which is still the first thing that moves.");
            Assert.That(plan.DepartureHours[VehicleTripPlan.TowedLegBoardAtDestination],
                        Is.EqualTo(Home).Within(1e-4f));

            for (int i = 0; i < plan.BlockCount; i++)
                Assert.That(plan.DepartureHours[i], Is.InRange(0f, 24f), $"block {i} departs off the day");
        }

        [Test]
        public void HerDriverWalksToTheReleaseAndThenAlongToTheDoor()
        {
            VehicleTripPlan plan = BuiltPlan(out string problem);
            Assert.That(plan, Is.Not.Null, problem);

            ScheduledLegs driver = plan.DriverLegs;
            Assert.That(driver.LengthMetres[VehicleTripPlan.TowedLegCouple], Is.GreaterThan(0.5f),
                "the couple beat has no walk in it, so nothing is shown happening — the whole point of " +
                "the block is that the yard SEES the work (P3).");
            Assert.That(driver.LengthMetres[VehicleTripPlan.TowedLegBoardAtOrigin], Is.GreaterThan(0.1f),
                "he teleports from the handle to the door.");

            // The handle is the street-side release the PLAYER works, in her own metres — so his walk
            // ends where the interactable is rather than at the middle of the truck.
            VehicleMeshDef tractor = Load(Aero);
            Vector2 handle = driver.PointAt(VehicleTripPlan.TowedLegCouple,
                                            driver.LengthMetres[VehicleTripPlan.TowedLegCouple]);
            Vector2 expected = (Vector2)Outbound()[0] + VehicleCouplingMath.LocalOffsetToWorld(
                tractor.FifthWheel.ReleaseHandleLocal, 0f);
            Assert.That((handle - expected).magnitude, Is.LessThan(0.05f),
                $"his walk ends at {handle}, and the release handle is at {expected}.");
        }

        [Test]
        public void TheLegsRiseAfterThePinGoesInAndWindDownBeforeItComesOut()
        {
            VehicleTripPlan plan = BuiltPlan(out string problem);
            Assert.That(plan, Is.Not.Null, problem);

            Assert.That(At(plan, VehicleTripPlan.TowedLegRestAtOrigin, 0.5f).TrailerLegsUp,
                        Is.EqualTo(0f), "she rests on her kingpin instead of her shoes.");
            Assert.That(At(plan, VehicleTripPlan.TowedLegCouple, 0.5f).TrailerCoupled, Is.False,
                        "the pin is in before her driver has reached the handle.");

            VehicleTripPose boarding = At(plan, VehicleTripPlan.TowedLegBoardAtOrigin, 0.5f);
            Assert.That(boarding.TrailerCoupled, Is.True);
            Assert.That(boarding.TrailerLegsUp, Is.InRange(0.1f, 0.9f),
                "the gear snapped rather than winding — the crank takes its published time, which is " +
                "what the sidecar's 'nothing stops a game dragging grounded shoes' warning is about.");

            Assert.That(At(plan, VehicleTripPlan.TowedLegDriveOut, 0.5f).TrailerLegsUp, Is.EqualTo(1f));
            Assert.That(At(plan, VehicleTripPlan.TowedLegUncouple, 0.5f).TrailerLegsUp,
                        Is.InRange(0.1f, 0.9f), "she sets a trailer down without winding the legs.");
            Assert.That(At(plan, VehicleTripPlan.TowedLegAlightAtOrigin, 0.5f).TrailerCoupled, Is.False,
                "her driver walks away with the trailer still on the pin.");
        }

        [Test]
        public void SheNeverTurnsInEitherBay()
        {
            VehicleTripPlan plan = BuiltPlan(out string problem);
            Assert.That(plan, Is.Not.Null, problem);

            Vector2 leavingHome = At(plan, VehicleTripPlan.TowedLegRestAtOrigin, 0.5f).MachineDirection;
            foreach (int leg in new[] { VehicleTripPlan.TowedLegCouple, VehicleTripPlan.TowedLegBoardAtOrigin,
                                        VehicleTripPlan.TowedLegUncouple, VehicleTripPlan.TowedLegAlightAtOrigin })
                Assert.That(Vector2.Angle(At(plan, leg, 0.5f).MachineDirection, leavingHome),
                            Is.LessThan(0.01f),
                    $"she turned in the home bay during block {leg}. With a 53-footer on the pin that " +
                    "pivot sweeps the trailer through the bays either side of her.");

            Vector2 atFar = At(plan, VehicleTripPlan.TowedLegRestAtDestination, 0.5f).MachineDirection;
            foreach (int leg in new[] { VehicleTripPlan.TowedLegAlightAtDestination,
                                        VehicleTripPlan.TowedLegBoardAtDestination })
                Assert.That(Vector2.Angle(At(plan, leg, 0.5f).MachineDirection, atFar), Is.LessThan(0.01f),
                    $"she turned in the far bay during block {leg}.");
        }

        // =============================================================================================
        //  2. THE DAY HAS TO CLOSE
        // =============================================================================================

        [Test]
        public void TheRoadHomeLeavesHerExactlyWhereTheRoadOutFoundHer()
        {
            VehicleTripPlan plan = BuiltPlan(out string problem);
            Assert.That(plan, Is.Not.Null, problem);

            // Where the road home sets her down, and where the road out picks her up. Nothing about a
            // trailer is saved, so those two are the SAME moment as far as tomorrow is concerned — both
            // read at a block boundary, where the travel is clamped and the answer is the table's own end.
            VehicleTripPose down = plan.SampleAt(
                DaySchedule.Wrap24(plan.DepartureHours[VehicleTripPlan.TowedLegUncouple] + 1e-4f));
            VehicleTripPose up = plan.SampleAt(
                DaySchedule.Wrap24(plan.DepartureHours[VehicleTripPlan.TowedLegDriveOut] + 1e-6f));

            Assert.That(down.LegIndex, Is.EqualTo(VehicleTripPlan.TowedLegUncouple));
            Assert.That(up.LegIndex, Is.EqualTo(VehicleTripPlan.TowedLegDriveOut));

            // The bar is a hand's breadth; the failure this guards is a trailer metres away and lying
            // the other way round, which is what an out-and-back does.
            Assert.That((down.TrailerPosition - up.TrailerPosition).magnitude, Is.LessThan(0.05f),
                $"she is set down at {down.TrailerPosition} and picked up at {up.TrailerPosition} — a " +
                "pose plan that does not close teleports her at midnight rather than drifting, because " +
                "nothing here is integrated across a day.");
            Assert.That(Vector2.Angle(down.TrailerDirection, up.TrailerDirection), Is.LessThan(0.05f),
                "she is set down lying across the bay she was found in.");

            // And the resting pose the plan publishes is that settled one, not the authored seed.
            Assert.That((plan.TrailerRestPosition - down.TrailerPosition).magnitude,
                        Is.LessThan(1e-3f));
        }

        [Test]
        public void HerPinIsOnThePlateThroughBothDrivingBlocks()
        {
            VehicleMeshDef tractor = Load(Aero), trailer = Load(Trailers[3]);   // the 53, worst case
            VehicleTripPlan plan = VehicleTripPlan.Build(
                Spec(tractor, trailer, Outbound(), Return()), SecondsPerGameHour, out string problem);
            Assert.That(plan, Is.Not.Null, problem);

            var pinLocal = new Vector2(trailer.Kingpin.CouplingPointLocal.x,
                                       trailer.Kingpin.CouplingPointLocal.y);
            var plateLocal = new Vector2(tractor.FifthWheel.CouplingPointLocal.x,
                                         tractor.FifthWheel.CouplingPointLocal.y);

            foreach (int leg in new[] { VehicleTripPlan.TowedLegDriveOut, VehicleTripPlan.TowedLegDriveHome })
                for (float t = 0f; t <= 1f; t += 0.02f)
                {
                    VehicleTripPose pose = At(plan, leg, t);
                    Assert.That(pose.TrailerCoupled, Is.True, $"block {leg} at t={t:0.00}: not coupled.");

                    float trailerHeading = BoatKinematics.BearingDegrees(pose.TrailerDirection);
                    float tractorHeading = BoatKinematics.BearingDegrees(pose.MachineDirection);
                    Vector2 pin = pose.TrailerPosition +
                                  VehicleCouplingMath.LocalOffsetToWorld(pinLocal, trailerHeading);
                    Vector2 plate = pose.MachinePosition +
                                    VehicleCouplingMath.LocalOffsetToWorld(plateLocal, tractorHeading);

                    Assert.That((pin - plate).magnitude, Is.LessThan(0.01f),
                        $"block {leg} at t={t:0.00}: her pin is {(pin - plate).magnitude:0.000} m off " +
                        "the plate. A coupled pair is welded at the pin in every frame.");
                }
        }

        [Test]
        public void TwoBuildsOfTheSameSpecPoseHerIdentically()
        {
            VehicleMeshDef tractor = Load(Classic), trailer = Load(Trailers[2]);
            VehicleTripPlan a = VehicleTripPlan.Build(Spec(tractor, trailer, Outbound(), Return()),
                                                      SecondsPerGameHour, out _);
            VehicleTripPlan b = VehicleTripPlan.Build(Spec(tractor, trailer, Outbound(), Return()),
                                                      SecondsPerGameHour, out _);
            Assert.That(a, Is.Not.Null); Assert.That(b, Is.Not.Null);

            for (float hour = 0f; hour < 24f; hour += 0.13f)
            {
                VehicleTripPose x = a.SampleAt(hour), y = b.SampleAt(hour);
                Assert.That(x.TrailerPosition, Is.EqualTo(y.TrailerPosition),
                    $"{hour:0.00}h: two builds of one spec put her in different places. The whole plan " +
                    "is a pure function of the hour (rule 5), settling included.");
                Assert.That(x.TrailerDirection, Is.EqualTo(y.TrailerDirection));
            }
        }

        // =============================================================================================
        //  3. WHAT IS REFUSED, AND WHY — each by name, each with the miss measured
        // =============================================================================================

        [Test]
        public void AnOrdinaryThereAndBackIsRefused_ACoupledPairCannotTurnInABay()
        {
            VehicleMeshDef tractor = Load(Aero), trailer = Load(Trailers[0]);
            VehicleTripPlan plan = VehicleTripPlan.Build(
                Spec(tractor, trailer, Outbound(), ThereAndBack()), SecondsPerGameHour,
                out string problem);

            Assert.That(plan, Is.Null,
                "an out-and-back on one road was accepted as a towing trip. She arrives at each bay on " +
                "the reverse of the heading she left it on, and the eight-block plan solves that by " +
                "pivoting her about her own centre — which with a trailer on the pin sweeps it through " +
                "the neighbouring bays.");
            Assert.That(problem, Does.Contain("cannot turn in a bay"));
            Assert.That(problem, Does.Contain("home"),
                "the refusal has to name WHICH end, or it cannot be acted on.");
            Assert.That(problem, Does.Contain("pull-through"));
        }

        [Test]
        public void AFarBayThatIsNotAPullThroughIsRefused()
        {
            // Home is a proper circuit; the far end doubles back on itself.
            Vector2[] home = Return();
            home[1] = new Vector2(20f, 80f);        // straight back the way she came in
            VehicleTripPlan plan = VehicleTripPlan.Build(
                Spec(Load(Aero), Load(Trailers[0]), Outbound(), home), SecondsPerGameHour,
                out string problem);

            Assert.That(plan, Is.Null, "the far bay needed a pivot and the plan took it.");
            Assert.That(problem, Does.Contain("far"));
            Assert.That(problem, Does.Contain("cannot turn in a bay"));
        }

        [Test]
        public void ATrailerStoodOffThePlateIsRefused_AndTheRefusalSaysByHowMuch()
        {
            VehicleMeshDef tractor = Load(Aero), trailer = Load(Trailers[0]);
            Vector2 wrong = CoupleReady(tractor, trailer, Outbound()[0], 0f) + new Vector2(3.5f, 0f);

            VehicleTripPlan plan = VehicleTripPlan.Build(
                Spec(tractor, trailer, Outbound(), Return(), wrong), SecondsPerGameHour,
                out string problem);

            Assert.That(plan, Is.Null, "a trailer three and a half metres off the slot was hooked.");
            Assert.That(problem, Does.Contain("not on her plate"));
            Assert.That(problem, Does.Contain("centreline"),
                "the refusal must carry the miss in metres — 'it did not couple' is not actionable.");
        }

        [Test]
        public void ATrailerLyingAcrossTheBayIsRefused()
        {
            VehicleMeshDef tractor = Load(Aero), trailer = Load(Trailers[0]);
            float across = 30f;   // well outside the slot's own 8.53° window
            Vector2 bay = CoupleReady(tractor, trailer, Outbound()[0], across);

            VehicleTripPlan plan = VehicleTripPlan.Build(
                Spec(tractor, trailer, Outbound(), Return(), bay, across), SecondsPerGameHour,
                out string problem);

            Assert.That(plan, Is.Null, "a trailer lying 30° across the bay was hooked.");
            Assert.That(problem, Does.Contain("across"));
        }

        [Test]
        public void AMachineWithNoFifthWheelAndABodyWithNoPinAreBothRefused()
        {
            VehicleMeshDef box = Load(Box), trailer = Load(Trailers[0]), aero = Load(Aero);

            VehicleTripPlan noPlate = VehicleTripPlan.Build(
                Spec(box, trailer, Outbound(), Return(), Vector2.zero), SecondsPerGameHour,
                out string a);
            Assert.That(noPlate, Is.Null, "a box truck towed a semi-trailer.");
            Assert.That(a, Does.Contain("no fifth wheel"));

            var noPin = new VehicleTripSpec(Outbound(), Return(), new Vector2(-6f, -4f), Vector2.right,
                                            new Vector2(66f, 86f), Vector2.left, aero.DriveDoorLocal,
                                            Out, Home, Cruise, Walk,
                                            new VehicleTowedSpec(aero.FifthWheel, box.Kingpin,
                                                                 Vector2.zero, 0f));
            Assert.That(VehicleTripPlan.Build(noPin, SecondsPerGameHour, out string b), Is.Null,
                "a box truck was towed by her nose.");
            Assert.That(b, Does.Contain("no kingpin"));
        }

        // =============================================================================================
        //  4. EVERY PAIR THE DROP SHIPPED
        // =============================================================================================

        [Test]
        public void EverySemiWillHaulEveryTrailerOffAPullThroughBay()
        {
            foreach (string tractorPath in new[] { Aero, Classic })
            foreach (string trailerPath in Trailers)
            {
                VehicleMeshDef tractor = Load(tractorPath), trailer = Load(trailerPath);
                VehicleTripPlan plan = VehicleTripPlan.Build(
                    Spec(tractor, trailer, Outbound(), Return()), SecondsPerGameHour,
                    out string problem);

                Assert.That(plan, Is.Not.Null,
                    $"{tractor.Id} could not haul {trailer.Id} off a pull-through bay: {problem}");

                // The couple beat has to land her INSIDE the accepting band, on the shipped test.
                Assert.That(VehicleCouplingMath.WouldCapture(
                                tractor.FifthWheel, trailer.Kingpin, Outbound()[0], 0f,
                                plan.TrailerRestPosition, plan.TrailerRestHeadingDegrees), Is.True,
                    $"{tractor.Id} + {trailer.Id}: the day settled her somewhere her own plate would " +
                    "not offer her tomorrow.");
            }
        }

        // =============================================================================================
        //  5. A SOLO RUN IS UNTOUCHED
        // =============================================================================================

        [Test]
        public void ARunWithNoTrailerStillRunsEightBlocksAndStillTurnsInTheBay()
        {
            var solo = new VehicleTripSpec(Outbound(), ThereAndBack(), new Vector2(-6f, -4f),
                                           Vector2.right, new Vector2(66f, 86f), Vector2.left,
                                           Load(Aero).DriveDoorLocal, Out, Home, Cruise, Walk);

            VehicleTripPlan plan = VehicleTripPlan.Build(solo, SecondsPerGameHour, out string problem);
            Assert.That(plan, Is.Not.Null, problem);
            Assert.That(plan.BlockCount, Is.EqualTo(VehicleTripPlan.LegCount));
            Assert.That(plan.Tows, Is.False);
            Assert.That(plan.SampleAt(plan.DepartureHours[VehicleTripPlan.LegDriveOut]).HasTrailer,
                        Is.False, "a solo run published trailer numbers, which a reader would believe.");

            // …and she still turns in the bay, which is the eight-block plan's whole answer to a
            // dead-end road and must not have been taken away by the towing work.
            Vector2 arrived = plan.SampleAt(plan.DepartureHours[VehicleTripPlan.LegBoardAtOrigin]).MachineDirection;
            Vector2 leaving = plan.SampleAt(plan.DepartureHours[VehicleTripPlan.LegDriveOut] - 1e-4f).MachineDirection;
            Assert.That(Vector2.Angle(arrived, leaving), Is.GreaterThan(90f),
                "she no longer comes about in the bay, so a solo truck would flip 180° in one frame.");
        }

        // ---- sampling helper ---------------------------------------------------------------------------

        /// <summary>The pose a fraction <paramref name="t"/> of the way through block
        /// <paramref name="leg"/> — by asking the plan for the block's own hours rather than guessing a
        /// clock, so a fixture cannot drift out of the block it means.</summary>
        static VehicleTripPose At(VehicleTripPlan plan, int leg, float t)
        {
            int next = (leg + 1) % plan.BlockCount;
            float length = DaySchedule.ElapsedHours(plan.DepartureHours[next], plan.DepartureHours[leg]);
            float inside = Mathf.Lerp(1e-5f, Mathf.Max(1e-5f, length - 1e-5f), Mathf.Clamp01(t));
            return plan.SampleAt(DaySchedule.Wrap24(plan.DepartureHours[leg] + inside));
        }
    }
}
