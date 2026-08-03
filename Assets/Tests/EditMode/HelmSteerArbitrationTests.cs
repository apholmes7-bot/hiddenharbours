using NUnit.Framework;
using HiddenHarbours.Boats;

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
    }
}
