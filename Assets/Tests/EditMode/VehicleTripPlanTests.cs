using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>A scheduled trip is a pure function of the clock</b> (CLAUDE.md rule 5) — the whole determinism
    /// contract for NPC drivers, headless.
    ///
    /// <para>The fixture is a synthetic 100 m road so that every number below is one you can check by
    /// hand; the region's real geometry is <c>NineMileCreekTripsTests</c>'s business. What is tested here
    /// is the RULE: the same hour gives the same pose for ever, mid-leg means mid-road, a save has
    /// nothing to save, and the machine does not flip round in a single frame at a bay.</para>
    /// </summary>
    public class VehicleTripPlanTests
    {
        // A 30-real-minute day, the shipped default: 75 real seconds per game hour.
        private const float SecondsPerGameHour = 1800f / 24f;

        private const float Cruise = 10f;   // m/s — 100 m of road is 10 s, a seventh of a game hour
        private const float Walk = 2f;      // m/s

        private static readonly Vector2 Bay = new(0f, 0f);
        private static readonly Vector2 FarBay = new(100f, 0f);

        private static VehicleTripSpec Spec(float outHour = 6f, float backHour = 18f) => new(
            outbound: new[] { Bay, FarBay },
            returnLeg: new[] { FarBay, Bay },
            originPost: new Vector2(0f, 4f),
            originPostFacing: Vector2.down,
            destinationPost: new Vector2(100f, 6f),
            destinationPostFacing: Vector2.down,
            doorLocal: new Vector2(-1.75f, 0.1f),
            outboundDepartureHour: outHour,
            returnDepartureHour: backHour,
            cruiseMetresPerSecond: Cruise,
            walkMetresPerSecond: Walk);

        private static VehicleTripPlan Built(float outHour = 6f, float backHour = 18f)
        {
            VehicleTripPlan plan = VehicleTripPlan.Build(Spec(outHour, backHour), SecondsPerGameHour,
                                                        out string problem);
            Assert.That(plan, Is.Not.Null, $"the fixture's own spec did not build: {problem}");
            return plan;
        }

        // =============================================================================================
        //  DETERMINISM — the rule the whole shape exists for
        // =============================================================================================

        [Test]
        public void TheSameHourGivesTheSamePoseEveryTime()
        {
            VehicleTripPlan a = Built();
            VehicleTripPlan b = Built();

            for (float h = 0f; h < 24f; h += 0.13f)
            {
                VehicleTripPose pa = a.SampleAt(h);
                VehicleTripPose pb = b.SampleAt(h);
                Assert.That(pb.MachinePosition, Is.EqualTo(pa.MachinePosition),
                    $"two plans built from the same spec disagree at {h:0.00}.");
                Assert.That(pb.LegIndex, Is.EqualTo(pa.LegIndex));
            }
        }

        [Test]
        public void SamplingIsIdempotentSoNothingAccumulates()
        {
            VehicleTripPlan plan = Built();
            // Walk the whole day forwards, then ask the FIRST hour again. A plan that integrated would
            // answer differently the second time; a pure one cannot.
            VehicleTripPose first = plan.SampleAt(6.2f);
            for (float h = 0f; h < 24f; h += 0.05f) plan.SampleAt(h);
            Assert.That(plan.SampleAt(6.2f).MachinePosition, Is.EqualTo(first.MachinePosition));
        }

        /// <summary>⭐ The save/load claim, stated as arithmetic: a region loaded mid-leg puts her ON the
        /// road, not at either bay, and re-deriving from the hour alone is enough to do it.</summary>
        [Test]
        public void MidDriveSheIsOnTheRoadAndNotAtEitherBay()
        {
            VehicleTripPlan plan = Built();
            float drivesAt = plan.DepartureHours[VehicleTripPlan.LegDriveOut];
            float halfway = drivesAt + plan.MachineLegs.TravelHours(VehicleTripPlan.LegDriveOut,
                                                                    SecondsPerGameHour) * 0.5f;

            VehicleTripPose pose = plan.SampleAt(halfway);
            Assert.That(pose.Stage, Is.EqualTo(VehicleTripStage.Driving));
            Assert.That(pose.Moving, Is.True);
            Assert.That(pose.MachinePosition.x, Is.EqualTo(50f).Within(1f),
                "half the drive should be half the road.");
            Assert.That(pose.DriverAboard, Is.True, "she is under way — her driver is in the cab.");
        }

        [Test]
        public void AtRestSheIsInHerBayWithNobodyAboard()
        {
            VehicleTripPlan plan = Built(outHour: 6f, backHour: 18f);
            VehicleTripPose night = plan.SampleAt(2f);

            Assert.That(night.Stage, Is.EqualTo(VehicleTripStage.Resting));
            Assert.That(night.MachinePosition, Is.EqualTo(Bay));
            Assert.That(night.DriverAboard, Is.False);
            Assert.That(night.Moving, Is.False);
        }

        [Test]
        public void ThroughTheWorkingDaySheStandsAtTheFarBayAndHerDriverIsAtHisPost()
        {
            VehicleTripPlan plan = Built(outHour: 6f, backHour: 18f);
            VehicleTripPose noon = plan.SampleAt(12f);

            Assert.That(noon.Stage, Is.EqualTo(VehicleTripStage.Resting));
            Assert.That(noon.MachinePosition, Is.EqualTo(FarBay));
            Assert.That(noon.DriverPosition, Is.EqualTo(new Vector2(100f, 6f)));
            Assert.That(noon.DriverAboard, Is.False);
        }

        // =============================================================================================
        //  THE DRIVER
        // =============================================================================================

        [Test]
        public void HerDriverWalksToTheDoorBeforeSheMoves()
        {
            VehicleTripPlan plan = Built(outHour: 6f);
            VehicleTripPose boarding = plan.SampleAt(6.001f);

            Assert.That(boarding.Stage, Is.EqualTo(VehicleTripStage.Boarding));
            Assert.That(boarding.Moving, Is.False, "she must not pull away before he is in.");
            Assert.That(boarding.DriverWalking, Is.True);
            Assert.That(boarding.DriverAboard, Is.False);
            Assert.That(boarding.MachinePosition, Is.EqualTo(Bay));
        }

        [Test]
        public void TheDoorHeWalksToIsOffToOneSideOfTheTruckNotAtHerCentre()
        {
            VehicleTripPlan plan = Built(outHour: 6f);
            float boardHours = plan.DriverLegs.TravelHours(VehicleTripPlan.LegBoardAtOrigin,
                                                           SecondsPerGameHour);
            VehicleTripPose arrived = plan.SampleAt(6f + boardHours * 0.999f);

            float offCentre = Vector2.Distance(arrived.DriverPosition, Bay);
            Assert.That(offCentre, Is.GreaterThan(1f),
                "the door came off the mesh at (−1.75, 0.10) — a driver standing ON the bay centre "
                + "means the door local was dropped and he is walking into the middle of the truck.");
        }

        [Test]
        public void HeIsBackOnHisFeetAtTheFarPostAfterSheParks()
        {
            VehicleTripPlan plan = Built(outHour: 6f);
            float rests = plan.DepartureHours[VehicleTripPlan.LegRestAtDestination];
            VehicleTripPose pose = plan.SampleAt(rests + 0.01f);

            Assert.That(pose.DriverAboard, Is.False);
            Assert.That(pose.DriverPosition, Is.EqualTo(new Vector2(100f, 6f)));
            Assert.That(pose.MachinePosition, Is.EqualTo(FarBay));
        }

        [Test]
        public void EveryHourOfTheDayHasExactlyOneLegAndItIsInRange()
        {
            VehicleTripPlan plan = Built();
            for (float h = 0f; h < 24f; h += 0.01f)
            {
                VehicleTripPose pose = plan.SampleAt(h);
                Assert.That(pose.LegIndex, Is.InRange(0, VehicleTripPlan.LegCount - 1),
                    $"no block covers {h:0.00} — the timetable has a hole in it.");
            }
        }

        // =============================================================================================
        //  THE TURN IN THE BAY — she must not flip round in one frame
        // =============================================================================================

        /// <summary>
        /// ⭐ The measured trap: there is one road into a bay and one out, so the way she arrived is the
        /// reverse of the way she leaves, and a posed body cannot back out. Without the turn she snaps
        /// 180° in a single frame, twice a day, at both ends.
        /// </summary>
        [Test]
        public void SheTurnsRoundInTheBayOverTheBoardingBlockRatherThanInOneFrame()
        {
            VehicleTripPlan plan = Built(outHour: 6f);
            float boardsAt = plan.DepartureHours[VehicleTripPlan.LegBoardAtOrigin];
            float block = plan.DriverLegs.TravelHours(VehicleTripPlan.LegBoardAtOrigin, SecondsPerGameHour);
            Assert.That(block, Is.GreaterThan(0f), "the fixture's driver must have a walk to make.");

            Vector2 arriving = plan.SampleAt(boardsAt - 0.001f).MachineDirection;
            Vector2 middle = plan.SampleAt(boardsAt + block * 0.5f).MachineDirection;
            Vector2 leaving = plan.SampleAt(boardsAt + block * 1.001f).MachineDirection;

            Assert.That(Vector2.Dot(arriving, leaving), Is.LessThan(-0.9f),
                "the fixture's road is a there-and-back, so arriving and leaving MUST be opposite — "
                + "otherwise this test cannot see a flip at all.");
            Assert.That(Mathf.Abs(Vector2.Dot(middle, arriving)), Is.LessThan(0.5f),
                $"halfway through boarding she is pointing {middle} — she should be square to both ends "
                + "of the turn, not still facing the way she arrived (no turn) or already facing out "
                + "(a one-frame flip).");
        }

        [Test]
        public void SheDrivesOutNoseFirstAndComesHomeNoseFirst()
        {
            VehicleTripPlan plan = Built();

            float outAt = plan.DepartureHours[VehicleTripPlan.LegDriveOut];
            float outFor = plan.MachineLegs.TravelHours(VehicleTripPlan.LegDriveOut, SecondsPerGameHour);
            Vector2 goingOut = plan.SampleAt(outAt + outFor * 0.5f).MachineDirection;

            float homeAt = plan.DepartureHours[VehicleTripPlan.LegDriveHome];
            float homeFor = plan.MachineLegs.TravelHours(VehicleTripPlan.LegDriveHome, SecondsPerGameHour);
            Vector2 comingHome = plan.SampleAt(homeAt + homeFor * 0.5f).MachineDirection;

            Assert.That(goingOut.x, Is.GreaterThan(0.9f), "the road out runs east; her nose must too.");
            Assert.That(comingHome.x, Is.LessThan(-0.9f),
                "the road home runs west and she is NOT reversing down it — her nose must follow the "
                + "direction she is travelling.");
        }

        // =============================================================================================
        //  THE TIMETABLE — derived, not authored
        // =============================================================================================

        [Test]
        public void ALongerRoadArrivesLaterRatherThanLeavingEarlier()
        {
            VehicleTripPlan shortRun = Built();

            var longSpec = new VehicleTripSpec(
                new[] { Bay, new Vector2(400f, 0f) }, new[] { new Vector2(400f, 0f), Bay },
                new Vector2(0f, 4f), Vector2.down, new Vector2(400f, 6f), Vector2.down,
                new Vector2(-1.75f, 0.1f), 6f, 18f, Cruise, Walk);
            VehicleTripPlan longRun = VehicleTripPlan.Build(longSpec, SecondsPerGameHour, out _);

            Assert.That(longRun.DepartureHours[VehicleTripPlan.LegBoardAtOrigin],
                        Is.EqualTo(shortRun.DepartureHours[VehicleTripPlan.LegBoardAtOrigin]),
                        "the AUTHORED departure is the owner's and must not move.");
            Assert.That(longRun.DepartureHours[VehicleTripPlan.LegRestAtDestination],
                        Is.GreaterThan(shortRun.DepartureHours[VehicleTripPlan.LegRestAtDestination]),
                        "four times the road must arrive later, not depart earlier.");
        }

        [Test]
        public void ALongerDayStretchesTheDerivedHoursToo()
        {
            VehicleTripPlan quick = Built();
            VehicleTripPlan slow = VehicleTripPlan.Build(Spec(), SecondsPerGameHour * 4f, out _);

            float quickDrive = quick.MachineLegs.TravelHours(VehicleTripPlan.LegDriveOut, SecondsPerGameHour);
            float slowDrive = slow.MachineLegs.TravelHours(VehicleTripPlan.LegDriveOut, SecondsPerGameHour * 4f);

            Assert.That(slowDrive, Is.EqualTo(quickDrive / 4f).Within(1e-4f),
                "a game hour four times as long is a drive that takes a quarter of one.");
            Assert.That(slow.SecondsPerGameHour, Is.EqualTo(SecondsPerGameHour * 4f),
                "the plan must carry the day length it was built against, or nothing can notice it "
                + "changed.");
        }

        // =============================================================================================
        //  REFUSALS — a half-authored trip must stand still LOUDLY
        // =============================================================================================

        [Test]
        public void ARoadHomeThatDoesNotMeetTheRoadOutIsRefused()
        {
            var bad = new VehicleTripSpec(
                new[] { Bay, FarBay }, new[] { new Vector2(500f, 500f), Bay },
                Vector2.zero, Vector2.down, Vector2.zero, Vector2.down, Vector2.zero,
                6f, 18f, Cruise, Walk);

            Assert.That(VehicleTripPlan.Build(bad, SecondsPerGameHour, out string problem), Is.Null);
            Assert.That(problem, Does.Contain("home"), $"the refusal must say what is wrong: '{problem}'");
        }

        [Test]
        public void AZeroCruiseIsRefusedRatherThanShippedAsATruckThatNeverArrives()
        {
            var bad = new VehicleTripSpec(
                new[] { Bay, FarBay }, new[] { FarBay, Bay },
                Vector2.zero, Vector2.down, Vector2.zero, Vector2.down, Vector2.zero,
                6f, 18f, 0f, Walk);

            Assert.That(VehicleTripPlan.Build(bad, SecondsPerGameHour, out string problem), Is.Null);
            Assert.That(problem, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void AOnePointRouteIsRefused()
        {
            var bad = new VehicleTripSpec(
                new[] { Bay }, new[] { FarBay, Bay },
                Vector2.zero, Vector2.down, Vector2.zero, Vector2.down, Vector2.zero,
                6f, 18f, Cruise, Walk);

            Assert.That(VehicleTripPlan.Build(bad, SecondsPerGameHour, out _), Is.Null);
        }
    }
}
