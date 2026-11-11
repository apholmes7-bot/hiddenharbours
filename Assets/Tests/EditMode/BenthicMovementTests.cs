using System;
using HiddenHarbours.Fishing;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    public sealed class BenthicMovementTests
    {
        // A genuinely sloped 5 m bed segment: 3 m horizontal, 4 m elevation change.
        private static readonly BenthicSegment Bed = new BenthicSegment(
            new BenthicPoint(10, 20, -9), new BenthicPoint(13, 20, -5), true);
        private static readonly BenthicMovementProfile Lobster = new BenthicMovementProfile(
            BenthicMotion.WalkAndShelter, 1, 4, 2);
        private static readonly BenthicMovementProfile Crab = new BenthicMovementProfile(
            BenthicMotion.Scuttle, 2, 1, 1);
        private static readonly BenthicMovementProfile Clam = new BenthicMovementProfile(
            BenthicMotion.Sessile, 0, 0, 0);

        private static BenthicPose Sample(int seed, double time, BenthicMovementProfile profile,
            string id = "occupant.test")
        {
            Assert.IsTrue(BenthicMovement.TrySample(seed, id, time, in Bed, in profile, out var pose));
            return pose;
        }

        private static double Distance(BenthicPoint a, BenthicPoint b) => Math.Sqrt(
            (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) +
            (a.Elevation - b.Elevation) * (a.Elevation - b.Elevation));

        private static void Same(BenthicPose a, BenthicPose b)
        {
            Assert.AreEqual(a.Position.X, b.Position.X);
            Assert.AreEqual(a.Position.Y, b.Position.Y);
            Assert.AreEqual(a.Position.Elevation, b.Position.Elevation);
            Assert.AreEqual(a.FractionAlongSegment, b.FractionAlongSegment);
            Assert.AreEqual(a.Activity, b.Activity);
        }

        [Test]
        public void Clam_RemainsAtItsBurrowAcrossSeedsAndLongTimes()
        {
            foreach (double time in new[] { -1e12, -1.0, 0, 1, 1e6, 1e12 })
                for (int seed = 0; seed < 1000; seed++)
                {
                    var pose = Sample(seed, time, Clam);
                    Assert.AreEqual(0, Distance(Bed.Home, pose.Position));
                    Assert.AreEqual(0, pose.FractionAlongSegment);
                    Assert.AreEqual(BenthicActivity.Attached, pose.Activity);
                }
        }

        [Test]
        public void Lobster_WalksOutAndBackAndSheltersForTheAuthoredDuration()
        {
            int shelter = 0, walking = 0, resting = 0;
            bool outward = false, inward = false;
            var previous = Sample(17, 0, Lobster);
            // 16 s cycle sampled at every half-second, independent of its seeded phase.
            for (int i = 1; i <= 32; i++)
            {
                var pose = Sample(17, i * 0.5, Lobster);
                if (pose.Activity == BenthicActivity.Sheltering)
                {
                    shelter++;
                    Assert.AreEqual(0, Distance(Bed.Home, pose.Position));
                }
                if (pose.Activity == BenthicActivity.Walking) walking++;
                if (pose.Activity == BenthicActivity.Resting)
                {
                    resting++;
                    Assert.AreEqual(0, Distance(Bed.Away, pose.Position));
                }
                outward |= pose.FractionAlongSegment > previous.FractionAlongSegment;
                inward |= pose.FractionAlongSegment < previous.FractionAlongSegment;
                previous = pose;
            }
            Assert.AreEqual(8, shelter);
            Assert.AreEqual(20, walking);
            Assert.AreEqual(4, resting);
            Assert.IsTrue(outward);
            Assert.IsTrue(inward);
        }

        [Test]
        public void Crab_ScuttlesAndRestsWithoutBecomingASwimmingOrShelteringAnimal()
        {
            int moving = 0, resting = 0;
            // 7 s cycle, same 5 m route at 2 m per game second.
            for (int i = 0; i < 28; i++)
            {
                var pose = Sample(9, i * 0.25, Crab);
                if (pose.Activity == BenthicActivity.Scuttling) moving++;
                else
                {
                    Assert.AreEqual(BenthicActivity.Resting, pose.Activity);
                    resting++;
                }
            }
            Assert.AreEqual(20, moving);
            Assert.AreEqual(8, resting);
        }

        [TestCase(BenthicMotion.WalkAndShelter)]
        [TestCase(BenthicMotion.Scuttle)]
        public void Motion_IsContinuousSupportedAndSpeedBoundedIncludingEveryTurn(BenthicMotion motion)
        {
            var profile = motion == BenthicMotion.WalkAndShelter ? Lobster : Crab;
            const double dt = 0.013;
            var previous = Sample(71, -16, profile);
            for (int i = 1; i <= 4000; i++)
            {
                var pose = Sample(71, -16 + i * dt, profile);
                double u = pose.FractionAlongSegment;
                Assert.That(u, Is.InRange(0.0, 1.0));
                Assert.AreEqual(10 + 3 * u, pose.Position.X, 1e-12);
                Assert.AreEqual(20, pose.Position.Y, 1e-12);
                Assert.AreEqual(-9 + 4 * u, pose.Position.Elevation, 1e-12);
                Assert.LessOrEqual(Distance(previous.Position, pose.Position),
                    profile.SpeedMetresPerGameSecond * dt + 1e-11);
                previous = pose;
            }
        }

        [Test]
        public void AThousandSeeds_ReplayExactlyAfterUnrelatedQueriesAndAtLongTimes()
        {
            for (int seed = 0; seed < 1000; seed++)
                foreach (double time in new[] { -1e12, -0.125, 0, 86400.25, 1e12 + 0.125 })
                {
                    var first = Sample(seed, time, Lobster);
                    Sample(~seed, time * -0.3, Crab, "occupant.other");
                    Same(first, Sample(seed, time, Lobster));
                    Assert.That(first.FractionAlongSegment, Is.InRange(0.0, 1.0));
                    Assert.IsFalse(double.IsNaN(first.Position.Elevation));
                }
        }

        [Test]
        public void SeedAndStableOccupantId_IndependentlySetPermanentPhase()
        {
            var a = Sample(12, 7, Lobster);
            Assert.AreNotEqual(a.FractionAlongSegment, Sample(13, 7, Lobster).FractionAlongSegment);
            Assert.AreNotEqual(a.FractionAlongSegment, Sample(12, 7, Lobster, "occupant.other").FractionAlongSegment);
        }

        [Test]
        public void LongTimeReduction_PreservesSubsecondMovement()
        {
            var profile = new BenthicMovementProfile(BenthicMotion.Scuttle, 1, 0, 0);
            var a = Sample(4, 1e12, profile);
            var b = Sample(4, 1e12 + 0.125, profile);
            Assert.AreEqual(0.125, Distance(a.Position, b.Position), 1e-10);
            Same(a, Sample(4, 1e12 + 10, profile));
        }

        [TestCase(BenthicMotion.Sessile)]
        [TestCase(BenthicMotion.WalkAndShelter)]
        [TestCase(BenthicMotion.Scuttle)]
        public void UnknownSupport_IsRejectedForEveryKind(BenthicMotion motion)
        {
            var bed = new BenthicSegment(Bed.Home, Bed.Away, false);
            var profile = new BenthicMovementProfile(motion, 1, 1, 1);
            Assert.IsFalse(BenthicMovement.TrySample(1, "occupant.test", 0, in bed, in profile, out _));
        }

        [Test]
        public void ZeroLength_IsAnAttachmentButNotAMovingRoute()
        {
            var bed = new BenthicSegment(Bed.Home, Bed.Home, true);
            Assert.IsTrue(BenthicMovement.TrySample(1, "occupant.test", 0, in bed, in Clam, out var pose));
            Assert.AreEqual(0, Distance(Bed.Home, pose.Position));
            Assert.IsFalse(BenthicMovement.TrySample(1, "occupant.test", 0, in bed, in Lobster, out _));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        public void NonfiniteTimesAndCoordinates_AreRejected(double invalid)
        {
            Assert.IsFalse(BenthicMovement.TrySample(1, "occupant.test", invalid, in Bed, in Lobster, out _));
            var bed = new BenthicSegment(new BenthicPoint(0, invalid, -1), Bed.Away, true);
            Assert.IsFalse(BenthicMovement.TrySample(1, "occupant.test", 0, in bed, in Lobster, out _));
        }

        [Test]
        public void InvalidProfileIdentityAndOverflow_AreRejected()
        {
            foreach (var profile in new[] {
                new BenthicMovementProfile((BenthicMotion)99, 1, 1, 1),
                new BenthicMovementProfile(BenthicMotion.Scuttle, 0, 1, 1),
                new BenthicMovementProfile(BenthicMotion.Scuttle, -1, 1, 1),
                new BenthicMovementProfile(BenthicMotion.Scuttle, 1, -1, 1),
                new BenthicMovementProfile(BenthicMotion.Scuttle, 1, 1, double.NaN),
                new BenthicMovementProfile(BenthicMotion.Scuttle, double.PositiveInfinity, 1, 1),
                new BenthicMovementProfile(BenthicMotion.WalkAndShelter, 1, 0, 1),
                new BenthicMovementProfile(BenthicMotion.Scuttle, double.Epsilon, 1, 1),
                new BenthicMovementProfile(BenthicMotion.Scuttle, 1, double.MaxValue, double.MaxValue) })
                Assert.IsFalse(BenthicMovement.TrySample(1, "occupant.test", 0, in Bed, in profile, out _));
            Assert.IsFalse(BenthicMovement.TrySample(1, null, 0, in Bed, in Lobster, out _));
            Assert.IsFalse(BenthicMovement.TrySample(1, "", 0, in Bed, in Lobster, out _));
            var huge = new BenthicSegment(Bed.Home, new BenthicPoint(double.MaxValue, 0, 0), true);
            Assert.IsFalse(BenthicMovement.TrySample(1, "occupant.test", 0, in huge, in Lobster, out _));
        }

        [Test]
        public void WarmQueries_AllocateZeroManagedBytes()
        {
            for (int i = 0; i < 100; i++) Sample(1, i, Lobster);
            long before = GC.GetAllocatedBytesForCurrentThread();
            bool allValid = true;
            for (int i = 0; i < 1024; i++)
                allValid &= BenthicMovement.TrySample(1, "occupant.test", i, in Bed, in Lobster, out _);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.IsTrue(allValid);
            Assert.AreEqual(0, allocated);
        }
    }
}
