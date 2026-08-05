using NUnit.Framework;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The S4.5 eased key steer (<see cref="DevBoatInput.EaseSteer"/> /
    /// <see cref="DevBoatInput.ComposeSteer"/>) — owner ask 3: "the steering wheel needs to follow
    /// the turning from the arrow keys — gradual and smooth." The COMMAND is eased (never just the
    /// wheel graphic — a graphic-only ease would show less lock than the rudder has, a lying
    /// instrument), so these pins are on pure maths with INJECTED dt: PlayMode frame count is not
    /// time, and nothing here reads a clock.
    /// </summary>
    public class HelmSteerEaseTests
    {
        private const float Lock = 0.28f;   // the shipped default; any positive value obeys the same pins

        // ---- the walk itself -----------------------------------------------------------------------

        [Test]
        public void EaseSteer_WalksAtTheConfiguredRate()
        {
            // rate = 1/secondsToFullLock per second: half the lock time covers half the travel.
            float half = DevBoatInput.EaseSteer(0f, 1f, Lock * 0.5f, Lock);
            Assert.That(half, Is.EqualTo(0.5f).Within(1e-5f), "centre → half lock in half the time");
            Assert.That(DevBoatInput.EaseSteer(half, 1f, Lock * 0.5f, Lock), Is.EqualTo(1f).Within(1e-6f),
                        "…and the second half completes the travel");
        }

        [Test]
        public void EaseSteer_SettlesExactly_NeverOrbitsTheTarget()
        {
            // Within one step of the target it returns the TARGET — bit-exact — or the mirrored
            // wheel's change key would keep moving and the dash would repaint forever.
            float v = DevBoatInput.EaseSteer(0.999f, 1f, 0.016f, Lock);
            Assert.That(v, Is.EqualTo(1f), "snap-to-target within one step (exact, not within-epsilon)");
            Assert.That(DevBoatInput.EaseSteer(v, 1f, 0.016f, Lock), Is.EqualTo(1f), "…and it STAYS settled");
            Assert.That(DevBoatInput.EaseSteer(0.001f, 0f, 0.016f, Lock), Is.EqualTo(0f),
                        "release settles exactly at centre too");
        }

        [Test]
        public void EaseSteer_ReversalPassesThroughCentre_Smoothly()
        {
            // Full port → full starboard sweeps the whole span at the same rate (≈2× the lock time):
            // after exactly one lock-time it has reached the centre, not jumped past it.
            float v = DevBoatInput.EaseSteer(-1f, 1f, Lock, Lock);
            Assert.That(v, Is.EqualTo(0f).Within(1e-5f), "one lock-time of reversal lands at centre");
            v = DevBoatInput.EaseSteer(v, 1f, Lock, Lock);
            Assert.That(v, Is.EqualTo(1f).Within(1e-5f), "…and one more completes the sweep");
        }

        [Test]
        public void ZeroSecondsToLock_IsTheInstantPreS45Snap()
        {
            // 0 = instant — the documented off switch, and what a stale GameConfig.asset row (a
            // serialized struct missing the new member deserializes it as 0) degrades to: the old
            // behaviour, never a broken one.
            Assert.That(DevBoatInput.EaseSteer(0f, 1f, 0.016f, 0f), Is.EqualTo(1f));
            Assert.That(DevBoatInput.EaseSteer(0.7f, -1f, 0.016f, 0f), Is.EqualTo(-1f));
        }

        [Test]
        public void Sabotage_InstantSnapRegression_IsDetected()
        {
            // The permanent arm (the #406 style): if the ease ever stops depending on dt — someone
            // "simplifies" it back to the momentary snap — this trips. A small dt must NOT reach a
            // distant target.
            float v = DevBoatInput.EaseSteer(0f, 1f, 0.01f, Lock);
            if (v >= 1f)
                Assert.Fail("SABOTAGE NOT DETECTED: key steer reached full lock in one 10 ms step — " +
                            "the ease has been reduced to the pre-S4.5 momentary snap and the wheel " +
                            "teleports lock-to-lock again.");
            Assert.That(v, Is.EqualTo(0.01f / Lock).Within(1e-5f));
        }

        // ---- the composed per-frame decision (arbitration on RAW keys + the ease) -------------------

        [Test]
        public void LiveSession_ZeroKeys_HoldsTheWheel_AndKillsAnyEasedTail()
        {
            // The eased tail after a key release must never read as "real input": arbitration runs
            // on the RAW keys, so a zero read still preserves a held wheel session — and the ease
            // state SYNCS to the held steer, so the tail cannot fight the wheel next frame.
            float steer = DevBoatInput.ComposeSteer(0f, 0f, sessionActive: true, heldSteer: -0.6f,
                                                    easeFrom: 0.9f, dt: 0.016f, secondsToFullLock: Lock,
                                                    out float easeNext, out bool end);
            Assert.That(steer, Is.EqualTo(-0.6f), "the wheel's held steer survives the key layer's frame");
            Assert.That(end, Is.False, "the session stays live");
            Assert.That(easeNext, Is.EqualTo(-0.6f), "…and the eased tail dies (synced to the held steer)");
        }

        [Test]
        public void AKeyPress_BreaksTheSession_AndEasesFromTheWheelsHeldSteer()
        {
            // Taking the channel over from a held wheel must not snap: the walk starts at the
            // wheel's lock, not at the stale ease value.
            float steer = DevBoatInput.ComposeSteer(1f, 0f, sessionActive: true, heldSteer: -0.6f,
                                                    easeFrom: 0f, dt: 0.016f, secondsToFullLock: Lock,
                                                    out float easeNext, out bool end);
            Assert.That(end, Is.True, "a real key press ends the wheel session — one decisive handover");
            Assert.That(steer, Is.EqualTo(-0.6f + 0.016f / Lock).Within(1e-5f),
                        "the command walks from the HELD steer toward the key, one step");
            Assert.That(easeNext, Is.EqualTo(steer), "the ease state carries the walk");
        }

        [Test]
        public void NoSession_KeysEaseTowardLock_AndReleaseEasesBack()
        {
            float steer = DevBoatInput.ComposeSteer(1f, 0f, sessionActive: false, heldSteer: 0f,
                                                    easeFrom: 0f, dt: Lock * 0.25f, secondsToFullLock: Lock,
                                                    out float easeNext, out bool end);
            Assert.That(end, Is.False);
            Assert.That(steer, Is.EqualTo(0.25f).Within(1e-5f), "a quarter lock-time = a quarter of travel");

            // Release: keys stay momentary, but the return to centre is just as gradual.
            steer = DevBoatInput.ComposeSteer(0f, 0f, sessionActive: false, heldSteer: steer,
                                              easeFrom: easeNext, dt: Lock * 0.125f, secondsToFullLock: Lock,
                                              out easeNext, out end);
            Assert.That(steer, Is.EqualTo(0.125f).Within(1e-5f), "released keys ease back toward centre");
        }

        [Test]
        public void GamepadStick_PassesThroughUnEased_AndBreaksASession()
        {
            // Analog input is already progressive under the player's thumb — easing it only adds lag.
            float steer = DevBoatInput.ComposeSteer(0f, 0.4f, sessionActive: false, heldSteer: 0f,
                                                    easeFrom: 0f, dt: 0.016f, secondsToFullLock: Lock,
                                                    out float easeNext, out bool end);
            Assert.That(steer, Is.EqualTo(0.4f), "the stick's deflection is the command, verbatim");
            Assert.That(easeNext, Is.EqualTo(0.4f), "…and the ease state tracks it (no later snap)");

            // A stick deflection is a real input for the S2a arbitration, exactly as before.
            DevBoatInput.ComposeSteer(0f, 0.25f, sessionActive: true, heldSteer: -0.6f,
                                      easeFrom: -0.6f, dt: 0.016f, secondsToFullLock: Lock,
                                      out _, out end);
            Assert.That(end, Is.True, "a stick deflection still breaks the wheel session (keys win)");
        }

        [Test]
        public void KeysStillWinOverTheStick_WhenBothAreLive()
        {
            // The pre-S4.5 priority, untouched: a held key out-ranks the stick.
            float steer = DevBoatInput.ComposeSteer(-1f, 0.8f, sessionActive: false, heldSteer: 0f,
                                                    easeFrom: 0f, dt: Lock, secondsToFullLock: Lock,
                                                    out _, out _);
            Assert.That(steer, Is.EqualTo(-1f).Within(1e-5f),
                        "one full lock-time under a held key reaches the key's lock, not the stick's");
        }
    }
}
