using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// <b>The editor-only scene-load hold (<see cref="SceneLoadHold"/>) is bounded and stays out of players.</b>
    /// The wait itself is pure (progress, clock and pause are handed in), so its cap is checked here with a fake
    /// clock; the real hold runs only in PlayMode, inside the region loader and <c>SceneSettle</c>.
    /// </summary>
    public sealed class SceneLoadHoldTests
    {
        [Test]
        public void AReadThatIsAlreadyDoneIsNotWaitedOn()
        {
            int pauses = 0;
            SceneLoadHold.Outcome o = SceneLoadHold.WaitForRead(() => 0.9f, () => 0d, () => pauses++, 10f);
            Assert.That(o, Is.EqualTo(SceneLoadHold.Outcome.ReadDone));
            Assert.That(pauses, Is.Zero);
        }

        [Test]
        public void TheWaitEndsWhenTheReadIsDone()
        {
            float progress = 0f;
            double clock = 0d;
            int pauses = 0;
            SceneLoadHold.Outcome o = SceneLoadHold.WaitForRead(
                () => progress, () => clock,
                () => { pauses++; clock += 0.001; if (pauses == 200) progress = SceneLoadHold.ReadDoneProgress; },
                SceneLoadHold.CapSeconds);
            Assert.That(o, Is.EqualTo(SceneLoadHold.Outcome.ReadDone));
            Assert.That(pauses, Is.EqualTo(200));
        }

        [Test]
        public void AReadThatNeverFinishesRunsOutTheCapAndNoMore()
        {
            double clock = 0d;
            int pauses = 0;
            SceneLoadHold.Outcome o = SceneLoadHold.WaitForRead(
                () => 0.5f, () => clock, () => { pauses++; clock += 0.25; }, 2f);
            Assert.That(o, Is.EqualTo(SceneLoadHold.Outcome.TimedOut));
            Assert.That(clock, Is.EqualTo(2d).Within(1e-9), "the wait stops on the first check at or past the cap");
            Assert.That(pauses, Is.EqualTo(8));
        }

        [Test]
        public void ANegativeCapChecksOnceAndDoesNotWait()
        {
            int pauses = 0;
            SceneLoadHold.Outcome o = SceneLoadHold.WaitForRead(() => 0f, () => 0d, () => pauses++, -1f);
            Assert.That(o, Is.EqualTo(SceneLoadHold.Outcome.TimedOut));
            Assert.That(pauses, Is.Zero);
        }

        [Test]
        public void ALoadQueuedBehindAnotherOperationOrPastTheBudgetIsNotHeld()
        {
            Assert.That(SceneLoadHold.ShouldHold(0, 0, SceneLoadHold.MaxTimeouts), Is.True);
            Assert.That(SceneLoadHold.ShouldHold(1, 0, SceneLoadHold.MaxTimeouts), Is.False,
                        "a load behind another needs main-thread frames to start; holding would only run out the cap");
            Assert.That(SceneLoadHold.ShouldHold(0, SceneLoadHold.MaxTimeouts - 1, SceneLoadHold.MaxTimeouts), Is.True);
            Assert.That(SceneLoadHold.ShouldHold(0, SceneLoadHold.MaxTimeouts, SceneLoadHold.MaxTimeouts), Is.False);
        }

        [Test]
        public void AReadReachingTheThresholdAtTheCapWinsOverTimeout()
        {
            double elapsed = 0d;
            float progress = 0f;
            int pauses = 0;
            var outcome = SceneLoadHold.WaitForRead(() => progress, () => elapsed, () =>
            {
                pauses++;
                elapsed = SceneLoadHold.CapSeconds;
                progress = SceneLoadHold.ReadDoneProgress;
            }, SceneLoadHold.CapSeconds);
            Assert.That(outcome, Is.EqualTo(SceneLoadHold.Outcome.ReadDone));
            Assert.That(pauses, Is.EqualTo(1));
            Assert.That(elapsed, Is.EqualTo((double)SceneLoadHold.CapSeconds));
        }

        [Test]
        public void ASlowPauseCanOvershootTheCapButCannotStartAnotherPause()
        {
            double elapsed = 0d;
            int pauses = 0;
            var outcome = SceneLoadHold.WaitForRead(() => 0.5f, () => elapsed,
                () => { pauses++; elapsed += SceneLoadHold.CapSeconds * 0.75d; }, SceneLoadHold.CapSeconds);
            Assert.That(outcome, Is.EqualTo(SceneLoadHold.Outcome.TimedOut));
            Assert.That(pauses, Is.EqualTo(2));
            Assert.That(elapsed, Is.EqualTo(SceneLoadHold.CapSeconds * 1.5d),
                "the cap bounds the next observation, not how long the scheduler can delay a pause");
        }

        [TestCase(0f, SceneLoadHold.Outcome.TimedOut)]
        [TestCase(0.899f, SceneLoadHold.Outcome.TimedOut)]
        [TestCase(SceneLoadHold.ReadDoneProgress, SceneLoadHold.Outcome.ReadDone)]
        [TestCase(1f, SceneLoadHold.Outcome.ReadDone)]
        public void ZeroCapChecksProgressButDoesNotPause(float progress, SceneLoadHold.Outcome expected)
        {
            int pauses = 0;
            Assert.That(SceneLoadHold.WaitForRead(() => progress, () => 0d, () => pauses++, 0f),
                        Is.EqualTo(expected));
            Assert.That(pauses, Is.Zero);
        }

        [Test]
        public void CompletedReadDoesNotNeedAClockOrPause()
        {
            Assert.That(SceneLoadHold.WaitForRead(() => SceneLoadHold.ReadDoneProgress,
                () => throw new System.InvalidOperationException("clock must not be read"),
                () => throw new System.InvalidOperationException("must not pause"), SceneLoadHold.CapSeconds),
                Is.EqualTo(SceneLoadHold.Outcome.ReadDone));
        }

        [Test]
        public void DrainingTheQueueDoesNotRestoreASpentSessionBudget()
        {
            Assert.That(SceneLoadHold.ShouldHold(1, SceneLoadHold.MaxTimeouts - 1, SceneLoadHold.MaxTimeouts), Is.False);
            Assert.That(SceneLoadHold.ShouldHold(0, SceneLoadHold.MaxTimeouts - 1, SceneLoadHold.MaxTimeouts), Is.True,
                        "draining allows the last remaining hold");
            Assert.That(SceneLoadHold.ShouldHold(1, SceneLoadHold.MaxTimeouts, SceneLoadHold.MaxTimeouts), Is.False);
            Assert.That(SceneLoadHold.ShouldHold(0, SceneLoadHold.MaxTimeouts, SceneLoadHold.MaxTimeouts), Is.False,
                        "draining cannot refund a timeout");
            Assert.That(SceneLoadHold.ShouldHold(0, 0, 0), Is.False, "zero budget disables holding");
        }

        [Test]
        public void NullOperationIsNotHeldTrackedOrCharged()
        {
            int pending = SceneLoadHold.InFlight, held = SceneLoadHold.HeldLoads, timeouts = SceneLoadHold.Timeouts;
            Assert.That(SceneLoadHold.HoldUntilRead(null, out var outcome), Is.Null);
            Assert.That(outcome, Is.EqualTo(SceneLoadHold.Outcome.NotHeld));
            Assert.That(SceneLoadHold.InFlight, Is.EqualTo(pending));
            Assert.That(SceneLoadHold.HeldLoads, Is.EqualTo(held));
            Assert.That(SceneLoadHold.Timeouts, Is.EqualTo(timeouts));
        }

        [Test]
        public void TheWorstCaseStallStaysInsideTheHangWatchdog()
        {
            Assert.That(SceneLoadHold.CapSeconds, Is.GreaterThan(0f));
            Assert.That(SceneLoadHold.MaxTimeouts, Is.InRange(1, 5));
            Assert.That(SceneLoadHold.CapSeconds * SceneLoadHold.MaxTimeouts,
                        Is.LessThan(HangWatchdogFrozenAfterSeconds));
            Assert.That(SceneLoadHold.ReadDoneProgress, Is.EqualTo(0.9f));
        }

        /// <summary>The PlayMode hang watchdog's frozen-thread verdict (Tests.Support MainThreadPulse
        /// .FrozenAfterSeconds). This assembly does not reference Tests.Support; SceneSettlePlayTests pins the
        /// same bound against the real constant through SceneSettle, which forwards to SceneLoadHold.</summary>
        private const float HangWatchdogFrozenAfterSeconds = 60f;

        [Test]
        public void TrackingIsCompiledOutOfPlayers()
        {
            MethodInfo track = typeof(SceneLoadHold).GetMethod(nameof(SceneLoadHold.Track));
            Assert.That(track, Is.Not.Null);
            var cond = track.GetCustomAttribute<ConditionalAttribute>();
            Assert.That(cond, Is.Not.Null);
            Assert.That(cond.ConditionString, Is.EqualTo("UNITY_EDITOR"));
        }

        /// <summary>A [Conditional] call drops its argument expressions in a player build, so a scene operation
        /// issued INSIDE <c>Track(...)</c> would silently never happen in a player.</summary>
        [Test]
        public void NoGameCodeIssuesASceneOperationInsideTrack()
        {
            string code = Path.Combine(Application.dataPath, "_Project", "Code");
            var bad = new Regex(@"SceneLoadHold\.Track\([^)]*SceneManager\.");
            foreach (string file in Directory.GetFiles(code, "*.cs", SearchOption.AllDirectories))
                foreach (string line in File.ReadAllLines(file))
                    Assert.That(bad.IsMatch(line), Is.False, file + ": " + line.Trim());
        }

        [Test]
        public void TheRegionLoaderHoldsOnlyInTheEditor()
        {
            string path = Path.Combine(Application.dataPath, "_Project", "Code", "World", "RegionSceneLoader.cs");
            string src = File.ReadAllText(path);
            int call = src.IndexOf("SceneLoadHold.HoldUntilRead(op)", System.StringComparison.Ordinal);
            Assert.That(call, Is.GreaterThan(0), "the region loader applies the hold");
            int open = src.LastIndexOf("#if UNITY_EDITOR", call, System.StringComparison.Ordinal);
            int close = src.LastIndexOf("#endif", call, System.StringComparison.Ordinal);
            Assert.That(open, Is.GreaterThan(close), "the hold call sits inside #if UNITY_EDITOR");
            Assert.That(Regex.Matches(src, @"SceneManager\.LoadSceneAsync\(").Count, Is.EqualTo(1),
                        "every region load goes through the one held start");
        }
    }
}
