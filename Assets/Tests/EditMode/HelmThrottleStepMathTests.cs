using NUnit.Framework;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The stepped-and-held keyed throttle's pure maths (ADR 0025 S1, owner directive 2026-08-03):
    /// detent stepping (including off-notch starts after a mouse drag), hold-to-repeat with a delay,
    /// the neutral snap window, clamps, and the <see cref="ThrottleDetentModel"/> arithmetic it rides
    /// on. All EditMode + engine-free with explicit dts — no frame-time dependence.
    /// </summary>
    public class HelmThrottleStepMathTests
    {
        private const int Ahead = 4, Astern = 2;   // the GameConfig reference feel

        // ---- stepping from on-notch positions ----------------------------------------------------

        [Test]
        public void StepOnce_FromNeutral_WalksTheAheadLadderAndClamps()
        {
            float d = 0f;
            d = HelmThrottleStepMath.StepOnce(d, +1, Ahead, Astern);
            Assert.That(d, Is.EqualTo(0.25f).Within(1e-6f));
            d = HelmThrottleStepMath.StepOnce(d, +1, Ahead, Astern);
            Assert.That(d, Is.EqualTo(0.5f).Within(1e-6f));
            d = HelmThrottleStepMath.StepOnce(d, +1, Ahead, Astern);
            d = HelmThrottleStepMath.StepOnce(d, +1, Ahead, Astern);
            Assert.That(d, Is.EqualTo(1f).Within(1e-6f), "four presses reach full ahead");
            d = HelmThrottleStepMath.StepOnce(d, +1, Ahead, Astern);
            Assert.That(d, Is.EqualTo(1f).Within(1e-6f), "clamped at full ahead");
        }

        [Test]
        public void StepOnce_Astern_HasItsOwnShorterLadder_AndClamps()
        {
            float d = 0f;
            d = HelmThrottleStepMath.StepOnce(d, -1, Ahead, Astern);
            Assert.That(d, Is.EqualTo(-0.5f).Within(1e-6f), "two astern notches → first press is half");
            d = HelmThrottleStepMath.StepOnce(d, -1, Ahead, Astern);
            Assert.That(d, Is.EqualTo(-1f).Within(1e-6f), "second press reaches full astern");
            d = HelmThrottleStepMath.StepOnce(d, -1, Ahead, Astern);
            Assert.That(d, Is.EqualTo(-1f).Within(1e-6f), "clamped at full astern");
        }

        [Test]
        public void StepOnce_CrossesNeutralOneDetentAtATime()
        {
            // Coming down from ahead lands ON neutral, never skips into astern.
            float d = HelmThrottleStepMath.StepOnce(0.25f, -1, Ahead, Astern);
            Assert.That(d, Is.EqualTo(0f).Within(1e-6f));
            // And up from astern lands on neutral too.
            d = HelmThrottleStepMath.StepOnce(-0.5f, +1, Ahead, Astern);
            Assert.That(d, Is.EqualTo(0f).Within(1e-6f));
        }

        // ---- stepping from OFF-notch positions (the mouse left the lever between detents) ----------

        [Test]
        public void StepOnce_FromOffNotch_LandsOnTheAdjacentDetent_NotPastIt()
        {
            Assert.That(HelmThrottleStepMath.StepOnce(0.45f, +1, Ahead, Astern),
                        Is.EqualTo(0.5f).Within(1e-6f), "0.45 stepped ahead → the next notch above (0.5)");
            Assert.That(HelmThrottleStepMath.StepOnce(0.45f, -1, Ahead, Astern),
                        Is.EqualTo(0.25f).Within(1e-6f), "0.45 stepped astern → the next notch below (0.25)");
            Assert.That(HelmThrottleStepMath.StepOnce(0.1f, -1, Ahead, Astern),
                        Is.EqualTo(0f).Within(1e-6f), "just above neutral steps down onto neutral");
            Assert.That(HelmThrottleStepMath.StepOnce(-0.2f, +1, Ahead, Astern),
                        Is.EqualTo(0f).Within(1e-6f), "just below neutral steps up onto neutral");
        }

        [Test]
        public void StepMany_AppliesNetSteps_OneDetentAtATime()
        {
            Assert.That(HelmThrottleStepMath.StepMany(0f, 3, Ahead, Astern),
                        Is.EqualTo(0.75f).Within(1e-6f));
            Assert.That(HelmThrottleStepMath.StepMany(0f, 99, Ahead, Astern),
                        Is.EqualTo(1f).Within(1e-6f), "repeat catch-up clamps");
            Assert.That(HelmThrottleStepMath.StepMany(0.5f, -4, Ahead, Astern),
                        Is.EqualTo(-1f).Within(1e-6f), "net astern walks down through neutral to full astern");
        }

        // ---- hold-to-repeat ------------------------------------------------------------------------

        [Test]
        public void TickRepeat_PressEdge_StepsOnce_ThenNothingUntilTheDelay()
        {
            var s = new HeldRepeatState();
            Assert.That(HelmThrottleStepMath.TickRepeat(ref s, pressedEdge: true, held: true,
                        dt: 0.016f, repeatDelaySec: 0.35f, repeatPerSec: 3f), Is.EqualTo(1), "the press itself");
            int extra = 0;
            for (float t = 0f; t < 0.30f; t += 0.05f)
                extra += HelmThrottleStepMath.TickRepeat(ref s, false, true, 0.05f, 0.35f, 3f);
            Assert.That(extra, Is.EqualTo(0), "held under the delay: no repeats yet");
        }

        [Test]
        public void TickRepeat_HeldPastTheDelay_WalksAtTheConfiguredRate()
        {
            var s = new HeldRepeatState();
            int total = HelmThrottleStepMath.TickRepeat(ref s, true, true, 0.05f, 0.35f, 3f);
            // Hold for 1.35 s beyond the press in 0.05 s ticks: repeats span the 1.0 s past the
            // delay → 1 (at the delay) + 3 (one per 1/3 s) = 4 repeats, 5 steps total.
            for (int i = 0; i < 27; i++)
                total += HelmThrottleStepMath.TickRepeat(ref s, false, true, 0.05f, 0.35f, 3f);
            Assert.That(total, Is.EqualTo(5).Within(1), "1 press + ~4 repeats over the held second");
        }

        [Test]
        public void TickRepeat_ZeroRate_IsEdgeOnly_AndReleaseResets()
        {
            var s = new HeldRepeatState();
            int total = HelmThrottleStepMath.TickRepeat(ref s, true, true, 0.05f, 0.35f, 0f);
            for (int i = 0; i < 100; i++)
                total += HelmThrottleStepMath.TickRepeat(ref s, false, true, 0.05f, 0.35f, 0f);
            Assert.That(total, Is.EqualTo(1), "rate 0 = the deliberate binnacle feel, one step per press");

            // Release, press again: a fresh edge steps again immediately.
            Assert.That(HelmThrottleStepMath.TickRepeat(ref s, false, false, 0.05f, 0.35f, 0f), Is.EqualTo(0));
            Assert.That(HelmThrottleStepMath.TickRepeat(ref s, true, true, 0.05f, 0.35f, 0f), Is.EqualTo(1));
        }

        // ---- the neutral snap window (mouse release) ------------------------------------------------

        [Test]
        public void ApplyNeutralSnap_InsideTheWindow_ClicksToNeutral_OutsideHolds()
        {
            Assert.That(HelmThrottleStepMath.ApplyNeutralSnap(0.05f, 0.08f), Is.EqualTo(0f));
            Assert.That(HelmThrottleStepMath.ApplyNeutralSnap(-0.08f, 0.08f), Is.EqualTo(0f), "window is inclusive");
            Assert.That(HelmThrottleStepMath.ApplyNeutralSnap(0.31f, 0.08f),
                        Is.EqualTo(0.31f), "a real lever stays where you left it");
            Assert.That(HelmThrottleStepMath.ApplyNeutralSnap(-0.6f, 0.08f), Is.EqualTo(-0.6f));
        }

        // ---- the detent arithmetic underneath ------------------------------------------------------

        [Test]
        public void DetentModel_EndpointsReachExactlyPlusMinusOne_EvenAsymmetric()
        {
            Assert.That(ThrottleDetentModel.DriveFor(Ahead, Ahead, Astern), Is.EqualTo(1f));
            Assert.That(ThrottleDetentModel.DriveFor(-Astern, Ahead, Astern), Is.EqualTo(-1f));
            Assert.That(ThrottleDetentModel.DriveFor(0, Ahead, Astern), Is.EqualTo(0f));
            Assert.That(ThrottleDetentModel.NearestNotch(1f, Ahead, Astern), Is.EqualTo(Ahead));
            Assert.That(ThrottleDetentModel.NearestNotch(-1f, Ahead, Astern), Is.EqualTo(-Astern));
        }
    }
}
