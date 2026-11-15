using System;
using HiddenHarbours.Tests.Support;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The hang watchdog's main-thread pulse (<see cref="MainThreadPulse"/>): a hang verdict must say whether the
    /// main thread had stopped or was still turning frames, and name the scenes it was among. The hook and the
    /// thread live in the PlayMode assembly; the decision and the wording are proven here on a fixed clock.
    /// </summary>
    public class MainThreadPulseTests
    {
        static readonly DateTime T0 = new DateTime(2026, 11, 15, 17, 47, 51, DateTimeKind.Utc);

        [Test]
        public void NoFrameEverSeen_SaysUnknown_AndIsNotFrozen()
        {
            var pulse = new MainThreadPulse();
            Assert.That(pulse.IsFrozen(T0.AddHours(1)), Is.False, "no hook, no claim");
            Assert.That(pulse.Describe(T0), Does.StartWith("Main thread: no frame was recorded"));
            Assert.That(pulse.Describe(T0), Does.Contain("Loaded scenes: (none seen). Last scene event: (none seen)."));
        }

        [Test]
        public void ARecentFrame_ReadsAsTurning()
        {
            var pulse = new MainThreadPulse();
            pulse.Tick(T0, 2835926, 1f);
            var now = T0.AddSeconds(MainThreadPulse.FrozenAfterSeconds - 1);
            Assert.That(pulse.IsFrozen(now), Is.False);
            Assert.That(pulse.Describe(now), Does.StartWith("Main thread TURNING: last frame 2835926 was 59 s ago (timeScale 1)."));
        }

        [Test]
        public void NoFrameForTheFrozenLimit_ReadsAsStopped_WithTheLastFrame()
        {
            var pulse = new MainThreadPulse();
            pulse.Tick(T0, 2835926, 0f);
            var now = T0.AddSeconds(608.4);
            Assert.That(pulse.IsFrozen(now), Is.True);
            Assert.That(pulse.Describe(now), Does.StartWith(
                "Main thread STOPPED: no frame for 608 s (last frame 2835926 at 2026-11-15T17:47:51Z, timeScale 0)."));
        }

        [Test]
        public void TheLatestFrameWins()
        {
            var pulse = new MainThreadPulse();
            pulse.Tick(T0, 10, 1f);
            pulse.Tick(T0.AddSeconds(500), 11, 1f);
            Assert.That(pulse.IsFrozen(T0.AddSeconds(520)), Is.False);
        }

        [Test]
        public void TheLastSceneEvent_AndTheLoadedList_AreNamed()
        {
            var pulse = new MainThreadPulse();
            pulse.Tick(T0, 1, 1f);
            pulse.SceneEvent("loaded StPeters (Single)", "StPeters*", T0.AddSeconds(-4));
            Assert.That(pulse.Describe(T0.AddSeconds(1)), Does.EndWith(
                " Loaded scenes: StPeters*. Last scene event: loaded StPeters (Single) at 2026-11-15T17:47:47Z."));
        }

        [Test]
        public void ARefresh_UpdatesTheList_AndKeepsTheLastEvent()
        {
            var pulse = new MainThreadPulse();
            pulse.Tick(T0, 1, 1f);
            pulse.SceneEvent("loaded StPeters (Single)", "StPeters*", T0.AddSeconds(-4));
            pulse.SceneEvent(null, "StPeters*, StPeters (loading)", T0);
            Assert.That(pulse.Describe(T0.AddSeconds(1)), Does.EndWith(
                " Loaded scenes: StPeters*, StPeters (loading). Last scene event: loaded StPeters (Single) at 2026-11-15T17:47:47Z."));
        }
    }
}
