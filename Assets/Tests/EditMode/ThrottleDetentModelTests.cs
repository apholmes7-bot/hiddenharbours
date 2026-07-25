using NUnit.Framework;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The notched single-lever throttle (<see cref="ThrottleDetentModel"/>) — the fix for the old on/off
    /// keyboard throttle. Verifies the headline behaviours: Up/Down STEP a HELD detent, the detent clamps
    /// at each end, neutral is exactly 0, both endpoints normalise to ±1 even with asymmetric notch counts
    /// (shorter reverse travel), the position PERSISTS between reads, and the drag/inverse path
    /// (<see cref="ThrottleDetentModel.NearestNotch"/> ↔ <see cref="ThrottleDetentModel.DriveFor"/>) round-
    /// trips. Pure model, no Unity runtime — the arithmetic is the whole test surface.
    /// </summary>
    public class ThrottleDetentModelTests
    {
        // ---- pure static arithmetic --------------------------------------------------------

        [Test]
        public void DriveFor_Endpoints_HitExactlyPlusMinusOne()
        {
            Assert.AreEqual(1f,  ThrottleDetentModel.DriveFor(4, 4, 2), 0f, "full ahead = +1");
            Assert.AreEqual(-1f, ThrottleDetentModel.DriveFor(-2, 4, 2), 0f, "full astern = -1 even with 2 astern notches");
            Assert.AreEqual(0f,  ThrottleDetentModel.DriveFor(0, 4, 2), 0f, "neutral = 0");
        }

        [Test]
        public void DriveFor_QuarterSteps_AndShorterReverse()
        {
            Assert.AreEqual(0.25f, ThrottleDetentModel.DriveFor(1, 4, 2), 1e-6f);
            Assert.AreEqual(0.50f, ThrottleDetentModel.DriveFor(2, 4, 2), 1e-6f);
            Assert.AreEqual(0.75f, ThrottleDetentModel.DriveFor(3, 4, 2), 1e-6f);
            Assert.AreEqual(-0.5f, ThrottleDetentModel.DriveFor(-1, 4, 2), 1e-6f, "one of two astern notches = half reverse");
        }

        [Test]
        public void Clamp_HoldsTheEnds()
        {
            Assert.AreEqual(4,  ThrottleDetentModel.Clamp(9,  2, 4), "clamps at full ahead");
            Assert.AreEqual(-2, ThrottleDetentModel.Clamp(-9, 2, 4), "clamps at full astern");
            Assert.AreEqual(3,  ThrottleDetentModel.Clamp(3,  2, 4), "interior notch unchanged");
        }

        [Test]
        public void NearestNotch_RoundsAndRoundTrips()
        {
            Assert.AreEqual(0,  ThrottleDetentModel.NearestNotch(0.10f, 4, 2));
            Assert.AreEqual(1,  ThrottleDetentModel.NearestNotch(0.26f, 4, 2));
            Assert.AreEqual(4,  ThrottleDetentModel.NearestNotch(0.90f, 4, 2), "past the last detent settles at full");
            Assert.AreEqual(-1, ThrottleDetentModel.NearestNotch(-0.60f, 4, 2));
            Assert.AreEqual(-2, ThrottleDetentModel.NearestNotch(-0.90f, 4, 2));

            // round-trip every detent through drive → nearest → same detent
            for (int n = -2; n <= 4; n++)
            {
                float d = ThrottleDetentModel.DriveFor(n, 4, 2);
                Assert.AreEqual(n, ThrottleDetentModel.NearestNotch(d, 4, 2), $"round-trip notch {n}");
            }
        }

        [Test]
        public void NearestNotch_ClampsOutOfRangeDrive()
        {
            Assert.AreEqual(4,  ThrottleDetentModel.NearestNotch(3f, 4, 2), "drive > 1 clamps to full ahead");
            Assert.AreEqual(-2, ThrottleDetentModel.NearestNotch(-3f, 4, 2), "drive < -1 clamps to full astern");
        }

        // ---- the stateful model (held detent) ----------------------------------------------

        [Test]
        public void Steps_ClampAtEachEnd()
        {
            var t = new ThrottleDetentModel(4, 2);
            for (int i = 0; i < 10; i++) t.StepAhead();
            Assert.AreEqual(4, t.Notch, "cannot step past full ahead");
            Assert.AreEqual(1f, t.Drive, 0f);

            for (int i = 0; i < 10; i++) t.StepAstern();
            Assert.AreEqual(-2, t.Notch, "cannot step past full astern");
            Assert.AreEqual(-1f, t.Drive, 0f);
        }

        [Test]
        public void Drive_HoldsPositionBetweenReads()
        {
            var t = new ThrottleDetentModel(4, 2);
            t.StepAhead();               // one notch
            float first = t.Drive;
            // no further input — a keyboard would drop to 0 here; the detent must NOT
            float second = t.Drive;
            float third  = t.Drive;
            Assert.AreEqual(0.25f, first, 1e-6f);
            Assert.AreEqual(first, second, 0f, "held throttle persists with no input");
            Assert.AreEqual(first, third, 0f);
        }

        [Test]
        public void Neutral_And_SnapToDrive()
        {
            var t = new ThrottleDetentModel(4, 2);
            t.StepAhead(); t.StepAhead();
            Assert.IsFalse(t.IsNeutral);
            t.ToNeutral();
            Assert.IsTrue(t.IsNeutral);
            Assert.AreEqual(0f, t.Drive, 0f);

            t.SnapToDrive(0.72f);        // a lever drag near the 3rd detent
            Assert.AreEqual(3, t.Notch);
            Assert.AreEqual(0.75f, t.Drive, 1e-6f);

            t.Reset();
            Assert.IsTrue(t.IsNeutral, "Reset (helm left unmanned) returns to neutral");
        }

        [Test]
        public void Constructor_ForcesAtLeastOneNotchEachWay()
        {
            var t = new ThrottleDetentModel(0, -3);
            Assert.AreEqual(1, t.AheadNotches);
            Assert.AreEqual(1, t.AsternNotches);
        }
    }
}
