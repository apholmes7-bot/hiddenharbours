using NUnit.Framework;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.UI.EditMode
{
    /// <summary>
    /// The wheel spin model's DRIFT GUARD (ADR 0025 S2a). The goldens are an INDEPENDENT float64
    /// transcription of the rig source <c>docs/art/rigs/ui/console-wheel/Art/wheelRig.js</c>
    /// (spin model :181-202, cell constants :26-31) computed in Python — never by running the C#
    /// under test, so a port bug cannot leak into its own expected values. Tolerance 1e-6 (two
    /// float64 transcriptions of one formula — bit equality is unattainable across libm exp()
    /// implementations, 1e-6 deg is ~8 orders below anything visible).
    ///
    /// <para>Negative-controlled through <see cref="WheelRigGeometry.StepWith"/>: perturbing the
    /// friction or the bounce restitution moves the sequence far past tolerance.</para>
    /// </summary>
    public class WheelSpinGoldenTests
    {
        private const double Tol = 1e-6;
        private const double Dt = 1.0 / 60.0;

        [Test]
        public void CellConstants_ArePinned()
        {
            // wheelRig.js:26-31 — cell + hub pivot, rim radii, knobs/spokes, hub boss.
            Assert.That(WheelRigGeometry.W, Is.EqualTo(256));
            Assert.That(WheelRigGeometry.H, Is.EqualTo(256));
            Assert.That(WheelRigGeometry.PVX, Is.EqualTo(128));
            Assert.That(WheelRigGeometry.PVY, Is.EqualTo(128));
            Assert.That(WheelRigGeometry.R, Is.EqualTo(92));
            Assert.That(WheelRigGeometry.RIMW, Is.EqualTo(15));
            Assert.That(WheelRigGeometry.KNOBS, Is.EqualTo(8));
            Assert.That(WheelRigGeometry.SPOKES, Is.EqualTo(8));
            Assert.That(WheelRigGeometry.KNOBL, Is.EqualTo(19));
            Assert.That(WheelRigGeometry.HUBR, Is.EqualTo(24));
            // Derived exactly as the JS derives them (Math.round(15/2) = 8):
            Assert.That(WheelRigGeometry.RI, Is.EqualTo(84), "rI = R - round(RIMW/2)");
            Assert.That(WheelRigGeometry.RO, Is.EqualTo(100), "rO = R + round(RIMW/2)");
            Assert.That(WheelRigGeometry.OUTER, Is.EqualTo(122), "outer = rO + KNOBL + 3");
            Assert.That(WheelRigGeometry.MaxRudder, Is.EqualTo(32.0), "wheelRig.js:182");
            Assert.That(WheelRigGeometry.DefaultTurns, Is.EqualTo(1.5));
            Assert.That(WheelRigGeometry.DefaultFriction, Is.EqualTo(2.4));
        }

        [Test]
        public void AngleMaps_MatchTheRigSource()
        {
            // wheelRig.js:183-187 (Python transcription).
            Assert.That(WheelRigGeometry.LockDeg(1.5), Is.EqualTo(540.0).Within(Tol));
            Assert.That(WheelRigGeometry.DegFromSteer(-1.0, 1.5), Is.EqualTo(-540.0).Within(Tol));
            Assert.That(WheelRigGeometry.DegFromSteer(-0.5, 1.5), Is.EqualTo(-270.0).Within(Tol));
            Assert.That(WheelRigGeometry.DegFromSteer(0.25, 1.5), Is.EqualTo(135.0).Within(Tol));
            Assert.That(WheelRigGeometry.DegFromSteer(2.0, 1.5), Is.EqualTo(540.0).Within(Tol), "clamped");
            Assert.That(WheelRigGeometry.SteerFromDeg(135.0, 1.5), Is.EqualTo(0.25).Within(Tol));
            Assert.That(WheelRigGeometry.Clicks(0), Is.EqualTo(0));
            Assert.That(WheelRigGeometry.Clicks(44.9), Is.EqualTo(0));
            Assert.That(WheelRigGeometry.Clicks(45.0), Is.EqualTo(1));
            Assert.That(WheelRigGeometry.Clicks(540.0), Is.EqualTo(12));
            Assert.That(WheelRigGeometry.Clicks(-540.0), Is.EqualTo(12), "clicks counts |deg|");
            Assert.That(WheelRigGeometry.RudderDeg(0.5), Is.EqualTo(16.0).Within(Tol));
        }

        // Coast from rest deg=0, vel=720 deg/s, dt=1/60, defaults (turns 1.5, friction 2.4, no
        // spring) — deg/vel per tick, Python transcription of wheelRig.js:189-202.
        private static readonly double[][] CoastGolden =
        {
            new[] { 11.529473269827879, 691.7683961896727 },
            new[] { 22.606869426467505, 664.6437693983777 },
            new[] { 33.24991466707339, 638.5827144363533 },
            new[] { 43.47564013466793, 613.543528055672 },
            new[] { 53.300409171603704, 589.4861422161467 },
            new[] { 62.73994350440234, 566.3720599679183 },
            new[] { 71.80934840187105, 544.1642938481222 },
            new[] { 80.52313684675534, 522.8273066930573 },
            new[] { 88.8952527596077, 502.32695477114214 },
            new[] { 96.93909331203537, 482.63043314566005 },
        };

        [Test]
        public void Step_CoastSequence_MatchesTheRigSource()
        {
            var s = new WheelRigGeometry.SpinState { Deg = 0.0, Vel = 720.0 };
            foreach (double[] row in CoastGolden)
            {
                s = WheelRigGeometry.Step(s, Dt, 1.5, 2.4, 0.0, out int hit);
                Assert.That(s.Deg, Is.EqualTo(row[0]).Within(Tol));
                Assert.That(s.Vel, Is.EqualTo(row[1]).Within(Tol));
                Assert.That(hit, Is.EqualTo(0), "no lock strike in the coast window");
            }
        }

        // Hard stop: deg=530, vel=400 into the +540 lock — the strike tick reports hit=+1 and the
        // bounce reverses a tenth of the velocity (wheelRig.js:197).
        private static readonly double[][] StopGolden =
        {
            new[] { 536.4052629276822, 384.31577566092926, 0 },
            new[] { 540.0, -36.92465385546543, 1 },
            new[] { 539.4087197088552, -35.4768174686863, 0 },
            new[] { 538.8406238495444, -34.085751558648454, 0 },
        };

        [Test]
        public void Step_HardStop_Bounces_AndReportsTheHitTick()
        {
            var s = new WheelRigGeometry.SpinState { Deg = 530.0, Vel = 400.0 };
            foreach (double[] row in StopGolden)
            {
                s = WheelRigGeometry.Step(s, Dt, 1.5, 2.4, 0.0, out int hit);
                Assert.That(s.Deg, Is.EqualTo(row[0]).Within(Tol));
                Assert.That(s.Vel, Is.EqualTo(row[1]).Within(Tol));
                Assert.That(hit, Is.EqualTo((int)row[2]));
            }
        }

        [Test]
        public void Step_SelfCentre_SnapsTheLastFraction()
        {
            // deg=0.5, vel=1.0, spring=3.0 → the centre snap window closes it out (js:200).
            var s = new WheelRigGeometry.SpinState { Deg = 0.5, Vel = 1.0 };
            s = WheelRigGeometry.Step(s, Dt, 1.5, 2.4, 3.0, out _);
            Assert.That(s.Deg, Is.EqualTo(0.0));
            Assert.That(s.Vel, Is.EqualTo(0.0));
        }

        [Test]
        public void Drag_WalksTheWheel_SetsReleaseVelocity_AndStopsAtLock()
        {
            // Console Wheel.dc.html:263-271 — the harness's own grab model.
            var s = new WheelRigGeometry.SpinState { Deg = 0.0, Vel = 0.0 };
            s = WheelRigGeometry.Drag(s, 10.0, Dt, 1.5, out int hit);
            Assert.That(s.Deg, Is.EqualTo(10.0).Within(Tol));
            Assert.That(s.Vel, Is.EqualTo(600.0).Within(Tol), "vel = d/dt at dt=1/60");
            Assert.That(hit, Is.EqualTo(0));

            // dt clamps up to the harness's 0.008 s floor before dividing.
            s = new WheelRigGeometry.SpinState { Deg = 0.0, Vel = 0.0 };
            s = WheelRigGeometry.Drag(s, 10.0, 0.001, 1.5, out _);
            Assert.That(s.Vel, Is.EqualTo(1250.0).Within(Tol));

            // A wrapped pointer delta (crossing atan2's seam) walks the short way.
            Assert.That(WheelRigGeometry.WrapDelta180(350.0), Is.EqualTo(-10.0).Within(Tol));
            Assert.That(WheelRigGeometry.WrapDelta180(-350.0), Is.EqualTo(10.0).Within(Tol));

            // Pinned at the lock: no movement, vel zeroed, the lock flash reports.
            s = new WheelRigGeometry.SpinState { Deg = 540.0, Vel = 0.0 };
            s = WheelRigGeometry.Drag(s, 5.0, Dt, 1.5, out hit);
            Assert.That(s.Deg, Is.EqualTo(540.0));
            Assert.That(s.Vel, Is.EqualTo(0.0));
            Assert.That(hit, Is.EqualTo(1));
        }

        [Test]
        public void QuantizeKey_IsTheRigsHalfDegreeCacheKey()
        {
            // wheelRig.js:167 — Math.round(deg*2): repaint granularity = 0.5°.
            Assert.That(WheelRigGeometry.QuantizeKey(0.0), Is.EqualTo(0));
            Assert.That(WheelRigGeometry.QuantizeKey(0.24), Is.EqualTo(0));
            Assert.That(WheelRigGeometry.QuantizeKey(0.25), Is.EqualTo(1));
            Assert.That(WheelRigGeometry.QuantizeKey(-0.26), Is.EqualTo(-1));
            Assert.That(WheelRigGeometry.QuantizeKey(540.0), Is.EqualTo(1080));
        }

        // ---- NEGATIVE CONTROL: prove the sequence goldens can fail --------------------------------

        [Test]
        public void NegativeControl_PerturbingTheSpinConstants_BreaksTheGoldens()
        {
            AssertPerturbationDetected(friction: 2.4 + 0.1, bounce: -0.10, name: "friction +0.1");
            AssertPerturbationDetected(friction: 2.4, bounce: -0.20, name: "bounce -0.20",
                                       fromStop: true);
        }

        private static void AssertPerturbationDetected(double friction, double bounce, string name,
                                                       bool fromStop = false)
        {
            var s = fromStop
                  ? new WheelRigGeometry.SpinState { Deg = 530.0, Vel = 400.0 }
                  : new WheelRigGeometry.SpinState { Deg = 0.0, Vel = 720.0 };
            double[][] golden = fromStop ? StopGolden : CoastGolden;
            double worst = 0.0;
            foreach (double[] row in golden)
            {
                s = WheelRigGeometry.StepWith(s, Dt, 1.5, friction, 0.0, bounce,
                                              WheelRigGeometry.VelDeadband, out _);
                double err = System.Math.Abs(s.Deg - row[0]) + System.Math.Abs(s.Vel - row[1]);
                if (err > worst) worst = err;
            }
            Assert.That(worst, Is.GreaterThan(Tol * 1e3),
                        $"{name} must be caught by the sequence goldens (worst error {worst:G4})");
        }
    }
}
