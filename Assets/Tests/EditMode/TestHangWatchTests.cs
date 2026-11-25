using System;
using HiddenHarbours.Tests.Support;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The PlayMode hang watchdog's bookkeeping (<see cref="TestHangWatch"/>): which test has overrun, by how
    /// much, and that it says so once. The thread and the exit live in the PlayMode assembly and can only be
    /// proven by a real hang on CI; everything they decide on is proven here, on a fixed clock.
    /// </summary>
    public class TestHangWatchTests
    {
        static readonly DateTime T0 = new DateTime(2026, 11, 14, 19, 29, 30, DateTimeKind.Utc);
        const string Ladders = "HiddenHarbours.Tests.PlayMode.NmcWharfWaterSidePlayTests.AllNineRegisteredLadders_AreClimbedDownAndUp_AtBothTides";

        [Test]
        public void NothingFires_BeforeTheRunStarts()
        {
            var watch = new TestHangWatch();
            Assert.That(watch.Check(T0.AddHours(3)), Is.Null);
            Assert.That(watch.Current, Is.Null);
        }

        [Test]
        public void ATestWithinItsLimit_DoesNotFire()
        {
            var watch = new TestHangWatch();
            watch.Started(Ladders, false, 0, T0);
            Assert.That(watch.Check(T0.AddSeconds(TestHangWatch.DefaultLimitSeconds)), Is.Null, "exactly at the limit is still within it");
        }

        [Test]
        public void ATestPastItsLimit_IsNamedWithItsRunTime()
        {
            var watch = new TestHangWatch();
            watch.Started(Ladders, false, 0, T0);
            string verdict = watch.Check(T0.AddSeconds(TestHangWatch.DefaultLimitSeconds + 1.5));
            Assert.That(verdict, Is.EqualTo("HANG: test " + Ladders + " has run 601 s (limit 600 s, since 2026-11-14T19:29:30Z)"));
        }

        [Test]
        public void TheVerdict_IsGivenOnce()
        {
            var watch = new TestHangWatch();
            watch.Started(Ladders, false, 0, T0);
            Assert.That(watch.Check(T0.AddHours(1)), Is.Not.Null);
            Assert.That(watch.Check(T0.AddHours(2)), Is.Null);
        }

        [Test]
        public void TheNextTest_RestartsTheClock()
        {
            var watch = new TestHangWatch();
            watch.Started("A.First", false, 0, T0);
            watch.Finished("A.First", false, T0.AddSeconds(500));
            watch.Started("A.Second", false, 0, T0.AddSeconds(501));
            Assert.That(watch.Check(T0.AddSeconds(900)), Is.Null, "A.Second has run 399 s");
            Assert.That(watch.Check(T0.AddSeconds(1102)), Does.StartWith("HANG: test A.Second has run 601 s"));
        }

        [Test]
        public void AHangAfterATestEnds_NamesTheGapAfterIt()
        {
            var watch = new TestHangWatch();
            watch.Started(Ladders, false, 0, T0);
            watch.Finished(Ladders, false, T0.AddSeconds(72));
            Assert.That(watch.Check(T0.AddSeconds(72 + 601)),
                Does.StartWith("HANG: the gap after test " + Ladders + " ended has run 601 s"));
        }

        [Test]
        public void ASuiteStart_IsNamedAsASuite()
        {
            var watch = new TestHangWatch();
            watch.Started("HiddenHarbours.Tests.PlayMode.NmcWharfWaterSidePlayTests", true, 0, T0);
            Assert.That(watch.Current, Is.EqualTo("suite HiddenHarbours.Tests.PlayMode.NmcWharfWaterSidePlayTests"));
            Assert.That(watch.Check(T0.AddSeconds(601)), Does.StartWith("HANG: suite "));
        }

        [Test]
        public void ARunThatNeverReachesItsFirstTest_IsNamed()
        {
            var watch = new TestHangWatch();
            watch.RunStarted(T0);
            Assert.That(watch.Check(T0.AddSeconds(601)), Does.StartWith("HANG: the start of the run, before any test began"));
        }

        [Test]
        public void AStoppedRun_NeverFires()
        {
            var watch = new TestHangWatch();
            watch.Started(Ladders, false, 0, T0);
            watch.Stop();
            Assert.That(watch.Check(T0.AddHours(3)), Is.Null);
            Assert.That(watch.Current, Is.Null);
        }

        [Test]
        public void ADeclaredTimeout_GetsItsOwnLimitPlusGrace()
        {
            var watch = new TestHangWatch();
            watch.Started("A.Slow", false, 1800000, T0); // [Timeout(1800000)]: 30 min, the suite's longest
            Assert.That(watch.Check(T0.AddSeconds(1800 + TestHangWatch.DeclaredTimeoutGraceSeconds)), Is.Null);
            Assert.That(watch.Check(T0.AddSeconds(1800 + TestHangWatch.DeclaredTimeoutGraceSeconds + 1)), Does.Contain("(limit 1920 s"));
        }

        [TestCase(0, TestHangWatch.DefaultLimitSeconds)]
        [TestCase(-5, TestHangWatch.DefaultLimitSeconds)]
        [TestCase(180000, TestHangWatch.DefaultLimitSeconds)] // 180 + 120 = 300 < 600: the default floor holds
        [TestCase(420000, TestHangWatch.DefaultLimitSeconds)] // 420 + 120 = 540 < 600
        [TestCase(900000, 1020)]
        [TestCase(1800000, 1920)]
        [TestCase(900001, 1021)] // a part second rounds up, never down
        public void LimitFor_IsTheLargerOfTheDefaultAndTheDeclaredTimeoutPlusGrace(int declaredMs, int expectedSeconds)
        {
            Assert.That(TestHangWatch.LimitFor(declaredMs), Is.EqualTo(expectedSeconds));
        }

        [Test]
        public void ASuite_IgnoresADeclaredTimeout()
        {
            var watch = new TestHangWatch();
            watch.Started("A", true, 1800000, T0);
            Assert.That(watch.Check(T0.AddSeconds(TestHangWatch.DefaultLimitSeconds + 1)), Is.Not.Null,
                "a fixture's [Timeout] bounds each of its tests, not its own setup");
        }

        [Test]
        public void TheHangExitCode_IsNotOneUnitysTestRunnerUses()
        {
            Assert.That(TestHangWatch.HangExitCode, Is.Not.EqualTo(0).And.Not.EqualTo(1).And.Not.EqualTo(2).And.Not.EqualTo(3));
        }
    }
}
