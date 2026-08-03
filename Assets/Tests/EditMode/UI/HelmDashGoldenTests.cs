using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.UI.EditMode
{
    /// <summary>
    /// The composed skiff dash's LAYOUT drift guard (ADR 0025 S2a). Geometry constants transcribed
    /// from BOTH helm rig sources — <c>consoleRig.js:152-169</c> and <c>sportRig.js:113-129</c>,
    /// identical by the rigs' own design note ("geometry IDENTICAL to consoleRig so the game treats
    /// it as an upgrade") — plus the COMPOSED mount positions (where the lever, wheel and dome
    /// compass land on the card), computed independently in Python from the rig formulas
    /// (console-helm/README.md:39-44 lever composite; compassRig.js:207-221 dome layout chain).
    /// 1e-6 on float chains, exact on the JS-rounded ints; negative-controlled.
    /// </summary>
    public class HelmDashGoldenTests
    {
        private const double Tol = 1e-6;

        [Test]
        public void DashGeometryConstants_ArePinned_AgainstBothHelmSources()
        {
            Assert.That(HelmDashGeometry.W, Is.EqualTo(600));
            Assert.That(HelmDashGeometry.TOPPAD, Is.EqualTo(40));
            Assert.That(HelmDashGeometry.H, Is.EqualTo(510));
            Assert.That(HelmDashGeometry.ConsoleX, Is.EqualTo(14));
            Assert.That(HelmDashGeometry.ConsoleY, Is.EqualTo(44));
            Assert.That(HelmDashGeometry.ConsoleW, Is.EqualTo(572));
            Assert.That(HelmDashGeometry.ConsoleH, Is.EqualTo(396));
            Assert.That(HelmDashGeometry.WheelCx, Is.EqualTo(300));
            Assert.That(HelmDashGeometry.WheelCy, Is.EqualTo(256));
            Assert.That(HelmDashGeometry.WheelR, Is.EqualTo(112));
            Assert.That(HelmDashGeometry.RpmCx, Is.EqualTo(104));
            Assert.That(HelmDashGeometry.RpmCy, Is.EqualTo(148));
            Assert.That(HelmDashGeometry.FuelCx, Is.EqualTo(496));
            Assert.That(HelmDashGeometry.FuelCy, Is.EqualTo(148));
            Assert.That(HelmDashGeometry.GaugeR, Is.EqualTo(56));
            Assert.That(HelmDashGeometry.BinnX, Is.EqualTo(430));
            Assert.That(HelmDashGeometry.BinnY, Is.EqualTo(284));
            Assert.That(HelmDashGeometry.BinnW, Is.EqualTo(154));
            Assert.That(HelmDashGeometry.BinnH, Is.EqualTo(156));
            Assert.That(HelmDashGeometry.DrivePx, Is.EqualTo(505));
            Assert.That(HelmDashGeometry.DrivePivotY, Is.EqualTo(416));
            Assert.That(HelmDashGeometry.DriveHitR, Is.EqualTo(46));
            Assert.That(HelmDashGeometry.DomeBoxX, Is.EqualTo(308));
            Assert.That(HelmDashGeometry.DomeBoxY, Is.EqualTo(-42));
            Assert.That(HelmDashGeometry.DomeBoxW, Is.EqualTo(124));
            Assert.That(HelmDashGeometry.DomeBoxH, Is.EqualTo(176));
            Assert.That(HelmDashGeometry.MaxSteerDeg, Is.EqualTo(45f));
        }

        [Test]
        public void AngleMaps_MatchTheHelmSources()
        {
            // consoleRig.js:144-149 (Python transcription).
            Assert.That(HelmDashGeometry.RpmAngleDeg(0.0), Is.EqualTo(-135.0).Within(Tol));
            Assert.That(HelmDashGeometry.RpmAngleDeg(0.555), Is.EqualTo(14.850000000000023).Within(Tol));
            Assert.That(HelmDashGeometry.RpmAngleDeg(1.0), Is.EqualTo(135.0).Within(Tol));
            Assert.That(HelmDashGeometry.RpmAngleDeg(2.0), Is.EqualTo(135.0).Within(Tol), "clamped");
            Assert.That(HelmDashGeometry.FuelPhiDeg(0.0), Is.EqualTo(-100.0).Within(Tol));
            Assert.That(HelmDashGeometry.FuelPhiDeg(0.5), Is.EqualTo(0.0).Within(Tol));
            Assert.That(HelmDashGeometry.FuelPhiDeg(1.0), Is.EqualTo(100.0).Within(Tol));
        }

        // ---- the composed mounts — the heart of S2a's "composition contract, not a picture" -------

        [Test]
        public void LeverMount_IsTheReadmesOwnComposite()
        {
            // console-helm/README.md:39-44: round(DRIVE.px − lv.px), round(DRIVE.pivotY + TOPPAD − lv.py)
            // = (505−116, 416+40−300) = (389, 156). Python-checked.
            HelmDashGeometry.LeverCellOrigin(out int x, out int y);
            Assert.That(x, Is.EqualTo(389));
            Assert.That(y, Is.EqualTo(156));
        }

        [Test]
        public void WheelMount_PutsTheHubOnTheDashWheelCentre()
        {
            // WheelRig mounts HUB-ON-POINT (console-wheel/README.md:74); the dash wheel centre is
            // (300, 256) + TOPPAD → hub (300, 296), cell origin (172, 168). Python-checked.
            HelmDashGeometry.WheelHub(out int hx, out int hy);
            Assert.That(hx, Is.EqualTo(300));
            Assert.That(hy, Is.EqualTo(296));
            HelmDashGeometry.WheelCellOrigin(out int cx, out int cy);
            Assert.That(cx, Is.EqualTo(172));
            Assert.That(cy, Is.EqualTo(168));
        }

        [Test]
        public void DomeBoxOnCard_ReachesIntoTheHeadroom()
        {
            // COMPASS.domeBox {308, −42, 124, 176} + TOPPAD → (308, −2): the crown unit rises above
            // the dash face into the TOPPAD headroom, exactly as the rig authors it.
            HelmDashGeometry.DomeBoxOnCard(out int x, out int y, out int w, out int h);
            Assert.That(x, Is.EqualTo(308));
            Assert.That(y, Is.EqualTo(-2));
            Assert.That(w, Is.EqualTo(124));
            Assert.That(h, Is.EqualTo(176));
        }

        [Test]
        public void DomeLayoutChain_AtTheConsoleCrownBox_MatchesTheCompassSource()
        {
            // compassRig.js:207-221 at box {308, −42, 124, 176} — the full derived chain,
            // Python-transcribed (fore 0.72; jsround = floor(x+0.5)).
            var L = CompassRigRender.ComputeDomeLayout(308, -42, 124, 176);
            Assert.That(L.Cx, Is.EqualTo(370));
            Assert.That(L.Gr, Is.EqualTo(52));
            Assert.That(L.Ry, Is.EqualTo(37));
            Assert.That(L.BaseH, Is.EqualTo(52));
            Assert.That(L.CapB, Is.EqualTo(19));
            Assert.That(L.UnitH, Is.EqualTo(155));
            Assert.That(L.Top, Is.EqualTo(-31), "Math.round(-31.5) rounds toward +inf = -31");
            Assert.That(L.Gy, Is.EqualTo(16));
            Assert.That(L.GBot, Is.EqualTo(53));
            Assert.That(L.BaseTop, Is.EqualTo(49));
            Assert.That(L.FootY, Is.EqualTo(101));
            Assert.That(L.Rt, Is.EqualTo(30));
            Assert.That(L.Rb, Is.EqualTo(51));
            Assert.That(L.CapT, Is.EqualTo(22));
            Assert.That(L.ArmR, Is.EqualTo(56));
        }

        [Test]
        public void SounderCutout_SlidesToPort_WhenTheDomeSharesTheBrow()
        {
            // consoleRig.js:389-401 (depth branch): bare brow (226,56,148,86); dome fitted slides
            // it to (170,56,126,86). Card-space y adds TOPPAD. S2 mounts the DepthRig here.
            HelmDashGeometry.SounderCutout(false, out int x, out int y, out int w, out int h);
            Assert.That((x, y, w, h), Is.EqualTo((226, 96, 148, 86)));
            HelmDashGeometry.SounderCutout(true, out x, out y, out w, out h);
            Assert.That((x, y, w, h), Is.EqualTo((170, 96, 126, 86)));
        }

        // ---- hit geometry --------------------------------------------------------------------------

        [Test]
        public void WheelHit_CatchesTheRim_NotThePanelBeyondThePad()
        {
            // Silhouette radius = OUTER (122) + the data pad (8 by default).
            Assert.That(HelmDashGeometry.IsOnWheel(new Vector2(300 + 122, 296), 8f), Is.True);
            Assert.That(HelmDashGeometry.IsOnWheel(new Vector2(300 + 130, 296), 8f), Is.True);
            Assert.That(HelmDashGeometry.IsOnWheel(new Vector2(300 + 131, 296), 8f), Is.False);
            Assert.That(HelmDashGeometry.IsOnWheel(new Vector2(300, 296), 8f), Is.True,
                        "the hub is part of the grab surface (dist 0)");
        }

        [Test]
        public void BinnacleHit_ScopesLeverClicks()
        {
            Assert.That(HelmDashGeometry.IsInBinnacle(new Vector2(505, 416 + 40)), Is.True);
            Assert.That(HelmDashGeometry.IsInBinnacle(new Vector2(300, 296)), Is.False,
                        "the wheel is not the binnacle");
            Assert.That(HelmDashGeometry.IsInBinnacle(new Vector2(429, 300 + 40)), Is.False, "left edge");
        }

        [Test]
        public void DashLeverMapping_RoundTripsThroughTheS1Inverse()
        {
            // The dash lever = the S1 lever with its hub at DRIVE (+TOPPAD): a pointer put exactly
            // on the grip at a sig maps back to that sig.
            foreach (float sig in new[] { -1f, -0.5f, 0f, 0.5f, 1f })
            {
                LeverRigGeometry.HandleOffset(sig, out double gx, out double gy);
                var p = new Vector2((float)(HelmDashGeometry.DrivePx + gx),
                                    (float)(HelmDashGeometry.DrivePivotY + HelmDashGeometry.TOPPAD + gy));
                Assert.That(HelmDashGeometry.DashLeverSigAt(p), Is.EqualTo(sig).Within(1e-6f));
                Assert.That(HelmDashGeometry.IsOnDashLeverGrip(p, sig, 30f), Is.True);
            }
        }

        // ---- NEGATIVE CONTROL ----------------------------------------------------------------------

        [Test]
        public void NegativeControl_PerturbingTheDomeChain_BreaksTheGoldens()
        {
            var L = CompassRigRender.ComputeDomeLayoutWith(308, -42, 124, 176, 0.72 + 0.05, 0.42, 0.40);
            Assert.That(L.Ry, Is.Not.EqualTo(37), "fore +0.05 must move the globe's vertical radius");
            var L2 = CompassRigRender.ComputeDomeLayoutWith(308, -42, 124, 176, 0.72, 0.42 + 0.03, 0.40);
            Assert.That(L2.Gr, Is.Not.EqualTo(52), "the width fraction must move the card radius");
        }
    }
}
