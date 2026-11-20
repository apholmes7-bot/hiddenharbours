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
