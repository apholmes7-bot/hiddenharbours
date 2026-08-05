using NUnit.Framework;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The S4.5 key-steer ease (<see cref="HelmSteerEase.Step"/>) — the owner's "the steering wheel
    /// needs to follow the turning from the arrow keys, gradual and smooth", implemented on the
    /// COMMAND so the dash wheel's mirror stays honest.
    ///
    /// <para>dt is injected, so the whole curve is pinned here without a frame loop — headless
    /// PlayMode frames are not time (<c>yield return null</c> advances a frame, not a clock), which is
    /// exactly why the maths lives in a pure static and only the wiring is integration-tested.</para>
    /// </summary>
    public class HelmSteerEaseTests
    {
        private const float Lock = 0.25f;   // the shipped GameConfig default: centre→full lock

        [Test]
        public void RateIsOneFullLockPerConfiguredSecond()
        {
            // Half the wind time from centre gets exactly half the lock.
            Assert.That(HelmSteerEase.Step(0f, 1f, Lock * 0.5f, Lock), Is.EqualTo(0.5f).Within(1e-6f));
            // And a tenth of it, a tenth of the lock — the rate does not depend on the distance left
            // (an exponential approach would slow down here; a wheel does not).
            Assert.That(HelmSteerEase.Step(0f, 1f, Lock * 0.1f, Lock), Is.EqualTo(0.1f).Within(1e-6f));
            Assert.That(HelmSteerEase.Step(0.4f, 1f, Lock * 0.1f, Lock), Is.EqualTo(0.5f).Within(1e-6f));
        }

        [Test]
        public void SettlesExactlyOnTarget_NeverAsymptotically()
        {
            // The overshooting step lands ON the target, not past it and not just short of it: the
            // wheel's half-degree repaint key must actually stop changing (rule 7).
            Assert.That(HelmSteerEase.Step(0.98f, 1f, Lock, Lock), Is.EqualTo(1f));
            Assert.That(HelmSteerEase.Step(1f, 1f, Lock, Lock), Is.EqualTo(1f));

            // Walked frame by frame from centre, it reaches the target BIT-EXACTLY and then stays.
            float v = 0f;
            for (int i = 0; i < 600; i++) v = HelmSteerEase.Step(v, 1f, 1f / 60f, Lock);
            Assert.That(v, Is.EqualTo(1f), "an exact settle, not 0.9999…");

            // Release: the same walk back to centre lands exactly on zero (a residual steer would
            // leave the boat crabbing and the wheel repainting forever).
            for (int i = 0; i < 600; i++) v = HelmSteerEase.Step(v, 0f, 1f / 60f, Lock);
            Assert.That(v, Is.EqualTo(0f));
        }

        [Test]
        public void ReversalPassesThroughCentreAtTheSameRate()
        {
            // Hard over to port, then full starboard demanded: the command must WIND through centre,
            // not snap across it. Lock-to-lock is two units, so it takes twice the wind time.
            float v = -1f;
            v = HelmSteerEase.Step(v, 1f, Lock, Lock);
            Assert.That(v, Is.EqualTo(0f).Within(1e-6f), "one wind time gets it to centre, no further");
            v = HelmSteerEase.Step(v, 1f, Lock, Lock);
            Assert.That(v, Is.EqualTo(1f).Within(1e-6f), "and the second gets it to the other lock");
        }

        [Test]
        public void NonPositiveWindTimeIsTheOldInstantStep()
        {
            // The owner's OFF switch: 0 restores the pre-S4.5 behaviour exactly, with no second code
            // path to keep in step (rule 6 — the feature is a dial, not a branch).
            Assert.That(HelmSteerEase.Step(0f, 1f, 1f / 60f, 0f), Is.EqualTo(1f));
            Assert.That(HelmSteerEase.Step(0f, -1f, 1f / 60f, -3f), Is.EqualTo(-1f));
        }

        [Test]
        public void ANonAdvancingFrameHoldsTheHelm()
        {
            // A paused or zero-length frame must not teleport the helm to the key's target.
            Assert.That(HelmSteerEase.Step(0.3f, 1f, 0f, Lock), Is.EqualTo(0.3f));
            Assert.That(HelmSteerEase.Step(0.3f, 1f, -1f, Lock), Is.EqualTo(0.3f));
        }

        [Test]
        public void TakingOverFromAWheelAngleStartsThere_NoSnap()
        {
            // The ease is stateless and reads the live steer back from its one owner, so "initialise
            // from current steer when the keys take the channel" is structural rather than a rule
            // someone has to remember: whatever the wheel left behind IS the starting point.
            const float wheelLeftIt = -0.62f;
            float first = HelmSteerEase.Step(wheelLeftIt, 1f, 1f / 60f, Lock);
            Assert.That(first, Is.EqualTo(wheelLeftIt + (1f / 60f) / Lock).Within(1e-6f),
                        "one frame's step FROM the wheel's angle — never a jump to the key target");
            Assert.That(first, Is.LessThan(0f), "and nowhere near lock after a single frame");
        }

        [Test]
        public void SABOTAGE_AnAsymptoticEaseMissesTheWindTime()
        {
            // A permanent negative control for the failure mode this file exists to prevent. The
            // tempting one-liner — Lerp(current, target, dt/seconds), an EXPONENTIAL approach —
            // passes every "it moves gradually" eyeball, but its rate falls off with the distance
            // left, so SteerEaseSeconds stops meaning anything: the helm is still well short of lock
            // when the dial says it should be there, and it creeps for many times longer.
            //
            // Pinning the MECHANISM (constant rate ⇒ arrives on time) rather than the symptom: an
            // "it never settles" assertion would rot, because in float32 the exponential does
            // eventually round onto the target — just nowhere near when it was asked to.
            const int Frames = 15;              // 15 frames at 60 fps IS the shipped 0.25 s wind time
            const float dt = Lock / Frames;

            float real = 0f, sabotage = 0f;
            for (int i = 0; i < Frames; i++)
            {
                real = HelmSteerEase.Step(real, 1f, dt, Lock);
                sabotage += (1f - sabotage) * (dt / Lock);
            }

            Assert.That(real, Is.EqualTo(1f), "the real ease reaches full lock in exactly its wind time");
            Assert.That(sabotage, Is.LessThan(0.8f),
                        "SABOTAGE NOT DETECTED: the exponential approach arrived on time too, so the " +
                        "constant-rate assertions above no longer discriminate between the two");
        }
    }
}
