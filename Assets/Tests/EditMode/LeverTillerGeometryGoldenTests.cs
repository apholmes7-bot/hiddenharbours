using NUnit.Framework;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The C# lever/tiller ports' DRIFT GUARD (ADR 0025 addendum — the honest S1 version: the full
    /// pixel golden-master needs the editor Canvas2D shim we are not building yet).
    ///
    /// <para><b>The goldens are an independent transcription of the rig source</b>
    /// (<c>docs/art/rigs/ui/lever-rig/Art/leverRig.js</c>): the committed table below was computed
    /// from the JS formulas (constants :22-29, <c>toView</c>/<c>toScreen</c> :38-45, <c>gripPt</c>
    /// :274-278) in float64 by a separate transcription (Python), NOT by running the C# under test —
    /// so a bug in the port cannot leak into its own expected values. Two transcriptions of the same
    /// float64 maths agree far tighter than the 1e-6 px tolerance used here (bit equality between
    /// transcriptions is unattainable in general; 1e-6 px is ~8 orders below a pixel).</para>
    ///
    /// <para><b>Negative-controlled:</b> perturbing any pinned constant through the parameterised
    /// core moves the offsets orders of magnitude past the tolerance, proving the goldens can
    /// actually fail (the "SABOTAGE NOT DETECTED" lesson — pin the mechanism).</para>
    /// </summary>
    public class LeverTillerGeometryGoldenTests
    {
        private const double Tol = 1e-6;   // px; two float64 transcriptions of the same formula

        // sig → hub angle (deg) → handleOffset dy (px, y-down; dx is 0 by construction — the swing
        // is fore/aft and projects onto the vertical axis). 9 samples = the rig's own strip ladder.
        private static readonly double[][] Golden =
        {
            //          sig     thetaDeg   dy
            new[] { -1.00,  56.00,  -58.7075850412 },
            new[] { -0.75,  47.75,  -80.9805260333 },
            new[] { -0.50,  39.50, -100.6011183907 },
            new[] { -0.25,  31.25, -117.0786738744 },
            new[] {  0.00,  23.00, -130.1016409228 },
            new[] {  0.25,  14.75, -139.5299041550 },
            new[] {  0.50,   6.50, -145.3732922547 },
            new[] {  0.75,  -1.75, -147.7621163358 },
            new[] {  1.00, -10.00, -146.9151785747 },
        };

        [Test]
        public void Lever_SigToAngleToHandleOffset_MatchesTheRigSource()
        {
            foreach (double[] row in Golden)
            {
                double sig = row[0];
                Assert.That(LeverRigGeometry.ThetaDeg(sig), Is.EqualTo(row[1]).Within(1e-9),
                            $"theta(sig={sig}) — leverRig.js:31 (TH0 - sig*THROW)");
                LeverRigGeometry.HandleOffset(sig, out double dx, out double dy);
                Assert.That(dx, Is.EqualTo(0.0).Within(Tol),
                            $"dx(sig={sig}) — the swing plane projects to x = PX exactly");
                Assert.That(dy, Is.EqualTo(row[2]).Within(Tol),
                            $"dy(sig={sig}) — leverRig.js:274-278 gripPt");
            }
        }

        [Test]
        public void Lever_SigFromOffset_InvertsTheGoldenPoints_Exactly()
        {
            // Every golden sig is one of the 201 table samples, so the nearest-sample inverse
            // (leverRig.js:294-300) returns it exactly. (Near the arc's apex at sig ≈ 0.81 two
            // travel points share a dy — the JS has the same ambiguity for NON-sample points;
            // sample points always win at distance zero.)
            foreach (double[] row in Golden)
            {
                LeverRigGeometry.HandleOffset(row[0], out double dx, out double dy);
                Assert.That(LeverRigGeometry.SigFromOffset(dx, dy),
                            Is.EqualTo((float)row[0]).Within(1e-6f), $"roundtrip at sig={row[0]}");
            }
        }

        [Test]
        public void Lever_CanvasDims_Pivot_AndSwingConstants_ArePinned()
        {
            // leverRig.js:22 — canvas + hub pivot; :25-29 — swing/camera; :67 — grip height.
            Assert.That(LeverRigGeometry.W, Is.EqualTo(232));
            Assert.That(LeverRigGeometry.H, Is.EqualTo(344));
            Assert.That(LeverRigGeometry.PX, Is.EqualTo(116));
            Assert.That(LeverRigGeometry.PY, Is.EqualTo(300));
            Assert.That(LeverRigGeometry.TH0, Is.EqualTo(23.0));
            Assert.That(LeverRigGeometry.THROW, Is.EqualTo(33.0));
            Assert.That(LeverRigGeometry.PITCH_DEG, Is.EqualTo(17.0));
            Assert.That(LeverRigGeometry.CAMD, Is.EqualTo(760.0));
            Assert.That(LeverRigGeometry.ARM, Is.EqualTo(178.0));
            Assert.That(LeverRigGeometry.GRIP_H, Is.EqualTo(145.0));
            Assert.That(LeverRigRender.W, Is.EqualTo(LeverRigGeometry.W), "render and geometry share the cell");
            Assert.That(LeverRigRender.H, Is.EqualTo(LeverRigGeometry.H));
        }

        [Test]
        public void Tiller_CanvasDims_Pivot_MaxSteer_AndHitBoxes_ArePinned()
        {
            // tillerRig.js:8 (cell + clamp pivot), :196 (maxSteer), :198 (hit targets).
            Assert.That(TillerRigRender.W, Is.EqualTo(120));
            Assert.That(TillerRigRender.H, Is.EqualTo(244));
            Assert.That(TillerRigRender.PivotX, Is.EqualTo(60));
            Assert.That(TillerRigRender.PivotY, Is.EqualTo(214));
            Assert.That(TillerRigRender.MaxSteerDeg, Is.EqualTo(45f));
            Assert.That(TillerRigRender.HitButtonX, Is.EqualTo(58));
            Assert.That(TillerRigRender.HitButtonY, Is.EqualTo(50));
            Assert.That(TillerRigRender.HitButtonR, Is.EqualTo(16));
            Assert.That(TillerRigRender.HitShiftX, Is.EqualTo(47));
            Assert.That(TillerRigRender.HitShiftY, Is.EqualTo(84));
            Assert.That(TillerRigRender.HitShiftW, Is.EqualTo(22));
            Assert.That(TillerRigRender.HitShiftH, Is.EqualTo(34));
        }

        // ---- NEGATIVE CONTROL: prove the goldens can fail ------------------------------------------

        [Test]
        public void NegativeControl_PerturbingEachConstant_BreaksTheGoldens()
        {
            // A half-degree on the neutral lean, one degree of throw, or 5% of camera distance each
            // move the grip offset far beyond the tolerance at multiple sigs — so a silently edited
            // constant CANNOT pass the golden table.
            AssertPerturbationDetected(th0: LeverRigGeometry.TH0 + 0.5, throwDeg: LeverRigGeometry.THROW,
                                       camD: LeverRigGeometry.CAMD, name: "TH0 +0.5°");
            AssertPerturbationDetected(th0: LeverRigGeometry.TH0, throwDeg: LeverRigGeometry.THROW + 1.0,
                                       camD: LeverRigGeometry.CAMD, name: "THROW +1°");
            AssertPerturbationDetected(th0: LeverRigGeometry.TH0, throwDeg: LeverRigGeometry.THROW,
                                       camD: LeverRigGeometry.CAMD * 1.05, name: "CAMD +5%");
        }

        private static void AssertPerturbationDetected(double th0, double throwDeg, double camD, string name)
        {
            double worst = 0.0;
            foreach (double[] row in Golden)
            {
                LeverRigGeometry.HandleOffsetWith(th0, throwDeg, LeverRigGeometry.PITCH_DEG, camD,
                                                  LeverRigGeometry.GRIP_H, LeverRigGeometry.GRIP_Z,
                                                  row[0], out _, out double dy);
                double err = System.Math.Abs(dy - row[2]);
                if (err > worst) worst = err;
            }
            Assert.That(worst, Is.GreaterThan(Tol * 1e3),
                        $"{name} must be caught by the golden table (worst dy error {worst:G4} px)");
        }
    }
}
