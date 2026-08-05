using NUnit.Framework;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The S2a wheel-vs-keys steer arbitration truth table (<see cref="DevBoatInput.ArbitrateSteer"/>):
    /// ONE steer owner (<c>BoatController._steer</c>), and one decisive handover between its two
    /// writers. Pinned in EditMode because headless batchmode drops key events, so the PlayMode
    /// key-press integration (<c>HelmDashPlayTests.ARealKeyPress…</c>) can only run when the
    /// environment can deliver a virtual key — this table is the always-on guard.
    /// </summary>
    public class HelmSteerArbitrationTests
    {
        [Test]
        public void NoSession_MomentaryKeysPassThrough_IncludingZero()
        {
            Assert.That(DevBoatInput.ArbitrateSteer(0f, false, 0.6f, out bool end), Is.EqualTo(0f),
                        "without a session the key layer centres the helm — S1 semantics untouched");
            Assert.That(end, Is.False);
            Assert.That(DevBoatInput.ArbitrateSteer(-1f, false, 0.6f, out end), Is.EqualTo(-1f));
            Assert.That(end, Is.False);
        }

        [Test]
        public void LiveSession_ZeroRead_PreservesTheWheelsHeldSteer()
        {
            Assert.That(DevBoatInput.ArbitrateSteer(0f, true, -0.6f, out bool end), Is.EqualTo(-0.6f),
                        "no key down → the wheel's held steer survives the key layer's frame");
            Assert.That(end, Is.False, "the session stays live");
        }

        [Test]
        public void LiveSession_ARealInput_WinsAndEndsTheSession()
        {
            Assert.That(DevBoatInput.ArbitrateSteer(1f, true, -0.6f, out bool end), Is.EqualTo(1f),
                        "keys win the channel");
            Assert.That(end, Is.True, "and the wheel session is broken — one decisive handover");
            // An analog stick's partial deflection is a real input too.
            Assert.That(DevBoatInput.ArbitrateSteer(0.25f, true, -0.6f, out end), Is.EqualTo(0.25f));
            Assert.That(end, Is.True);
        }

        // ---- S4.5: the key-steer ease must not leak into this table --------------------------------

        [Test]
        public void LiveSession_TheEasedTailAfterAKeyReleaseIsNotARealInput()
        {
            // S4.5 gave the keys an EASE: after A/D is released the commanded steer keeps walking
            // back toward centre for up to SteerEaseSeconds. That tail is this component's own
            // follow-through, and it is what a naive wiring would hand to ArbitrateSteer — where a
            // non-zero read means "keys win, break the wheel session". A player who lets go of a key
            // and then grabs the wheel would have the grab torn away by their own release.
            //
            // The fix is ordering, not a new branch: arbitration reads the RAW momentary key state,
            // which is already 0 the frame the key comes up. This pins the contract that makes the
            // ordering matter — a would-be eased tail value ends the session, so it must never be
            // what gets passed in.
            Assert.That(DevBoatInput.ArbitrateSteer(0f, true, -0.6f, out bool end), Is.EqualTo(-0.6f),
                        "the RAW read on a released key is zero → the wheel's held steer survives");
            Assert.That(end, Is.False, "and the session lives");

            const float easedTail = -0.42f;   // what the ease would still be commanding that frame
            Assert.That(DevBoatInput.ArbitrateSteer(easedTail, true, -0.6f, out end),
                        Is.EqualTo(easedTail));
            Assert.That(end, Is.True,
                        "SABOTAGE NOT DETECTED: feeding the eased tail in would be harmless, so the " +
                        "raw-read ordering in ReadEngine is no longer load-bearing — re-derive this");
        }

        [Test]
        public void LiveSession_HeldSteerPassesStraightThroughTheEase()
        {
            // The other half of the composition: while a session is live and no key is down, the
            // arbitrated value IS the wheel's held steer, and ReadEngine then eases FROM the live
            // steer TOWARD it. Same value both ends ⇒ the ease is a no-op and the two writers never
            // fight — the property that lets the ease read the one owner instead of keeping a copy.
            const float held = -0.6f;
            float arbitrated = DevBoatInput.ArbitrateSteer(0f, true, held, out _);
            Assert.That(HelmSteerEase.Step(held, arbitrated, 1f / 60f, 0.25f), Is.EqualTo(held),
                        "a live wheel session is passed through untouched, dt notwithstanding");
        }
    }
}
