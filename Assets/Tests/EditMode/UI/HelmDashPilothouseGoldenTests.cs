using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.UI.EditMode
{
    /// <summary>
    /// The composed PILOTHOUSE dashes' LAYOUT drift guard (ADR 0025 S4 — Novi + Cape Islander).
    /// Every figure here was recomputed INDEPENDENTLY in Python straight from the rig sources
    /// (<c>noviRig.js:120-145</c>, <c>capeRig.js:122-145</c>, <c>compassRig.js:186-236</c>), never
    /// read back out of the C# port — a reviewer recomputing a sample must land on the same numbers
    /// or the port is wrong. The same script reproduces the shipped S2a skiff dome golden
    /// (Cx 370, Gr 52, Ry 37, Top −31, Gy 16 — <see cref="HelmDashGoldenTests"/>) from the same
    /// transcription, which is what says the transcription itself is sound.
    ///
    /// <para>The two rigs share ONE geometry table by their own design note (noviRig.js:118-119:
    /// "KEPT dimensionally in step with capeRig"), so these goldens serve both renderers; what
    /// separates the two dashes is paint, and <see cref="NegativeControl_TheTwoHelmFamilies_DoNotShareALayout"/>
    /// is what stops the pilothouse quietly becoming the skiff wearing another name.</para>
    /// </summary>
    public class HelmDashPilothouseGoldenTests
    {
        // ---- the table ------------------------------------------------------------------------------

        [Test]
        public void PilothouseConstants_ArePinned_AgainstBothWheelhouseSources()
        {
            Assert.That(HelmDashGeometry.PilotW, Is.EqualTo(600));
            Assert.That(HelmDashGeometry.PilotTOPPAD, Is.EqualTo(54));
            Assert.That(HelmDashGeometry.PilotH, Is.EqualTo(548), "494 + TOPPAD (noviRig.js:15)");
            Assert.That(HelmDashGeometry.PilotShellX, Is.EqualTo(12));
            Assert.That(HelmDashGeometry.PilotShellY, Is.EqualTo(34));
            Assert.That(HelmDashGeometry.PilotShellW, Is.EqualTo(576));
            Assert.That(HelmDashGeometry.PilotShellH, Is.EqualTo(452));
            Assert.That(HelmDashGeometry.PilotFaceX, Is.EqualTo(26));
            Assert.That(HelmDashGeometry.PilotFaceY, Is.EqualTo(48));
            Assert.That(HelmDashGeometry.PilotFaceW, Is.EqualTo(548));
            Assert.That(HelmDashGeometry.PilotFaceH, Is.EqualTo(424));
            Assert.That(HelmDashGeometry.PilotWheelCx, Is.EqualTo(300));
            Assert.That(HelmDashGeometry.PilotWheelCy, Is.EqualTo(328));
            Assert.That(HelmDashGeometry.PilotWheelR, Is.EqualTo(104));
            Assert.That(HelmDashGeometry.PilotRpmCx, Is.EqualTo(92));
            Assert.That(HelmDashGeometry.PilotRpmCy, Is.EqualTo(214));
            Assert.That(HelmDashGeometry.PilotFuelCx, Is.EqualTo(508));
            Assert.That(HelmDashGeometry.PilotFuelCy, Is.EqualTo(214));
            Assert.That(HelmDashGeometry.PilotGaugeR, Is.EqualTo(50));
            Assert.That(HelmDashGeometry.PilotBinnX, Is.EqualTo(430));
            Assert.That(HelmDashGeometry.PilotBinnY, Is.EqualTo(296));
            Assert.That(HelmDashGeometry.PilotBinnW, Is.EqualTo(154));
            Assert.That(HelmDashGeometry.PilotBinnH, Is.EqualTo(180));
            Assert.That(HelmDashGeometry.PilotDrivePx, Is.EqualTo(507));
            Assert.That(HelmDashGeometry.PilotDrivePivotY, Is.EqualTo(456));
            Assert.That(HelmDashGeometry.PilotDriveHitR, Is.EqualTo(46));
            Assert.That(HelmDashGeometry.PilotBankX, Is.EqualTo(34));
            Assert.That(HelmDashGeometry.PilotBankY, Is.EqualTo(298));
            Assert.That(HelmDashGeometry.PilotRockColA, Is.EqualTo(50), "BANK.x + 16");
            Assert.That(HelmDashGeometry.PilotRockColB, Is.EqualTo(116), "BANK.x + 82");
            Assert.That(HelmDashGeometry.PilotRockRow0, Is.EqualTo(322));
            Assert.That(HelmDashGeometry.PilotIgnCx, Is.EqualTo(400));
            Assert.That(HelmDashGeometry.PilotIgnCy, Is.EqualTo(454));
            Assert.That(HelmDashGeometry.PilotIgnR, Is.EqualTo(17));
            Assert.That(HelmDashGeometry.PilotSlotX, Is.EqualTo(new[] { 52, 225, 398 }));
            Assert.That(HelmDashGeometry.PilotSlotW, Is.EqualTo(150));
            Assert.That(HelmDashGeometry.PilotSlotY, Is.EqualTo(18));
            Assert.That(HelmDashGeometry.PilotSlotH, Is.EqualTo(104));
            Assert.That(HelmDashGeometry.PilotBrowB, Is.EqualTo(122));
        }

        [Test]
        public void BothWheelhouseRigs_ResolveToTheSameCanvas()
        {
            foreach (ConsoleRigKind rig in new[] { ConsoleRigKind.Novi, ConsoleRigKind.Cape })
            {
                Assert.That(HelmDashGeometry.IsPilothouse(rig), Is.True);
                Assert.That(HelmDashGeometry.CanvasW(rig), Is.EqualTo(600));
                Assert.That(HelmDashGeometry.CanvasH(rig), Is.EqualTo(548));
                Assert.That(HelmDashGeometry.Toppad(rig), Is.EqualTo(54));
            }
        }

        // ---- the composed mounts ----------------------------------------------------------------------

        [Test]
        public void LeverMount_IsTheRigsOwnComposite()
        {
            // round(DRIVE.px − lv.px), round(DRIVE.pivotY + TOPPAD − lv.py)
            // = (507−116, 456+54−300) = (391, 210). Python-checked.
            HelmDashGeometry.LeverCellOrigin(ConsoleRigKind.Novi, out int x, out int y);
            Assert.That((x, y), Is.EqualTo((391, 210)));
            HelmDashGeometry.LeverCellOrigin(ConsoleRigKind.Cape, out x, out y);
            Assert.That((x, y), Is.EqualTo((391, 210)), "the Cape shares the Novi's binnacle exactly");
        }

        [Test]
        public void WheelMount_PutsTheHubOnTheWheelhouseWheelCentre()
        {
            // (300, 328) + TOPPAD → hub (300, 382); cell origin = hub − (128,128) = (172, 254).
            HelmDashGeometry.WheelHub(ConsoleRigKind.Novi, out int hx, out int hy);
            Assert.That((hx, hy), Is.EqualTo((300, 382)));
            HelmDashGeometry.WheelCellOrigin(ConsoleRigKind.Novi, out int cx, out int cy);
            Assert.That((cx, cy), Is.EqualTo((172, 254)));
        }

        [Test]
        public void CompassBoxes_LandWhereEachUnitMounts()
        {
            // domeBox {226,−50,148,182} + TOPPAD → (226, 4): the crown unit still reaches into the
            // headroom. flushBox {260,132,80,80} + TOPPAD → (260, 186), sunk into the dash face.
            HelmDashGeometry.DomeBoxOnCard(ConsoleRigKind.Novi, out int x, out int y, out int w, out int h);
            Assert.That((x, y, w, h), Is.EqualTo((226, 4, 148, 182)));
            HelmDashGeometry.FlushBoxOnCard(ConsoleRigKind.Cape, out x, out y, out w, out h);
            Assert.That((x, y, w, h), Is.EqualTo((260, 186, 80, 80)));

            // The compositor asks by MOUNT, and must get the matching box each way.
            HelmDashGeometry.CompassBoxOnCard(ConsoleRigKind.Novi, CompassMount.Dome,
                                              out x, out y, out w, out h);
            Assert.That((x, y, w, h), Is.EqualTo((226, 4, 148, 182)));
            HelmDashGeometry.CompassBoxOnCard(ConsoleRigKind.Novi, CompassMount.Flush,
                                              out x, out y, out w, out h);
            Assert.That((x, y, w, h), Is.EqualTo((260, 186, 80, 80)));
        }

        [Test]
        public void BrowSlotBoxes_PinAtBothBoxSizes()
        {
            // noviRig.js:135-136 slotBox(i, portrait), +TOPPAD. Landscape sits at SLOTY; portrait
            // rises to BROWB−150 = −28 → card y 26, up into the headroom. Python-checked.
            (int x, int y, int w, int h)[] landscape =
            {
                (52, 72, 150, 104), (225, 72, 150, 104), (398, 72, 150, 104),
            };
            (int x, int y, int w, int h)[] portrait =
            {
                (52, 26, 150, 150), (225, 26, 150, 150), (398, 26, 150, 150),
            };
            for (int i = 0; i < 3; i++)
            {
                HelmDashGeometry.SlotBoxOnCard(i, false, out int x, out int y, out int w, out int h);
                Assert.That((x, y, w, h), Is.EqualTo(landscape[i]), $"slot {i} landscape");
                HelmDashGeometry.SlotBoxOnCard(i, true, out x, out y, out w, out h);
                Assert.That((x, y, w, h), Is.EqualTo(portrait[i]), $"slot {i} portrait");
            }
        }

        [Test]
        public void TheDomeCompass_DisplacesTheCentreMount_TheFlushOneDoesNot()
        {
            // noviRig.js:453 — the crown unit takes the centre brow slot's space; the flush unit
            // sinks into the face, so the brow keeps all three.
            Assert.That(HelmDashGeometry.SlotIsDisplacedByCompass(1, CompassMount.Dome), Is.True);
            Assert.That(HelmDashGeometry.SlotIsDisplacedByCompass(0, CompassMount.Dome), Is.False);
            Assert.That(HelmDashGeometry.SlotIsDisplacedByCompass(2, CompassMount.Dome), Is.False);
            foreach (int i in new[] { 0, 1, 2 })
            {
                Assert.That(HelmDashGeometry.SlotIsDisplacedByCompass(i, CompassMount.Flush), Is.False);
                Assert.That(HelmDashGeometry.SlotIsDisplacedByCompass(i, CompassMount.None), Is.False);
            }
        }

        // ---- the compass chains, at more than one box size ---------------------------------------------

        [Test]
        public void FlushLayoutChain_MatchesTheCompassSource_AtTwoBoxSizes()
        {
            // compassRig.js:188 — cx,cy = round(x+w/2), round(y+h/2); R = floor(min(w,h)/2) − 2.
            CompassRigRender.ComputeFlushLayout(260, 186, 80, 80, out int cx, out int cy, out int r);
            Assert.That((cx, cy, r), Is.EqualTo((300, 226, 38)), "the pilothouse flush box");
            CompassRigRender.ComputeFlushLayout(10, 20, 100, 60, out cx, out cy, out r);
            Assert.That((cx, cy, r), Is.EqualTo((60, 50, 28)),
                        "a second, non-square box — the radius comes off the SHORTER side");
        }

        [Test]
        public void DomeLayoutChain_AtTheWheelhouseCrownBox_MatchesTheCompassSource()
        {
            // compassRig.js:207-236 at box {226, −50, 148, 182}. The pilothouse crown box is BIGGER
            // than the skiffs' (148×182 vs 124×176), so every derived figure moves. Python-checked.
            var L = CompassRigRender.ComputeDomeLayout(226, -50, 148, 182);
            Assert.That(L.Cx, Is.EqualTo(300));
            Assert.That(L.Gr, Is.EqualTo(62));
            Assert.That(L.Ry, Is.EqualTo(45));
            Assert.That(L.BaseH, Is.EqualTo(63));
            Assert.That(L.CapB, Is.EqualTo(22));
            Assert.That(L.UnitH, Is.EqualTo(185));
            Assert.That(L.Top, Is.EqualTo(-50), "unitH 185 > h 182, so the tall-box branch caps at y");
            Assert.That(L.Gy, Is.EqualTo(5));
            Assert.That(L.GBot, Is.EqualTo(50));
            Assert.That(L.BaseTop, Is.EqualTo(46));
            Assert.That(L.FootY, Is.EqualTo(109));
            Assert.That(L.Rt, Is.EqualTo(36));
            Assert.That(L.Rb, Is.EqualTo(61));
            Assert.That(L.CapT, Is.EqualTo(26));
            Assert.That(L.ArmR, Is.EqualTo(66));
        }

        // ---- hit geometry follows the rig --------------------------------------------------------------

        [Test]
        public void HitGeometry_TracksTheWheelhouse_NotTheSkiff()
        {
            // The binnacle sits 12 px lower and is 24 px deeper than the skiffs'.
            Assert.That(HelmDashGeometry.IsInBinnacle(ConsoleRigKind.Novi, new Vector2(507, 456 + 54)), Is.True);
            Assert.That(HelmDashGeometry.IsInBinnacle(ConsoleRigKind.Novi, new Vector2(429, 300 + 54)), Is.False,
                        "left edge");
            Assert.That(HelmDashGeometry.IsOnWheel(ConsoleRigKind.Novi, new Vector2(300, 382), 8f), Is.True);
            Assert.That(HelmDashGeometry.IsOnWheel(ConsoleRigKind.Novi, new Vector2(300 + 131, 382), 8f), Is.False);
        }

        [Test]
        public void DashLeverMapping_RoundTripsThroughTheS1Inverse_OnTheWheelhouse()
        {
            foreach (float sig in new[] { -1f, -0.5f, 0f, 0.5f, 1f })
            {
                LeverRigGeometry.HandleOffset(sig, out double gx, out double gy);
                var p = new Vector2((float)(HelmDashGeometry.PilotDrivePx + gx),
                                    (float)(HelmDashGeometry.PilotDrivePivotY + HelmDashGeometry.PilotTOPPAD + gy));
                Assert.That(HelmDashGeometry.DashLeverSigAt(ConsoleRigKind.Novi, p),
                            Is.EqualTo(sig).Within(1e-6f));
                Assert.That(HelmDashGeometry.IsOnDashLeverGrip(ConsoleRigKind.Novi, p, sig, 30f), Is.True);
            }
        }

        // ---- NEGATIVE CONTROL --------------------------------------------------------------------------

        [Test]
        public void NegativeControl_TheTwoHelmFamilies_DoNotShareALayout()
        {
            // #400's rule, both directions: the SKIFF layout must fail the pilothouse goldens and the
            // pilothouse layout must fail the skiff's. If a refactor ever collapsed the two tables into
            // one, every pair below would go equal and this test — not a proof sheet — catches it.
            Assert.That(HelmDashGeometry.CanvasH(ConsoleRigKind.Console), Is.EqualTo(510));
            Assert.That(HelmDashGeometry.CanvasH(ConsoleRigKind.Novi), Is.EqualTo(548));
            Assert.That(HelmDashGeometry.CanvasH(ConsoleRigKind.Console),
                        Is.Not.EqualTo(HelmDashGeometry.CanvasH(ConsoleRigKind.Novi)));

            HelmDashGeometry.LeverCellOrigin(ConsoleRigKind.Console, out int sx, out int sy);
            HelmDashGeometry.LeverCellOrigin(ConsoleRigKind.Novi, out int px, out int py);
            Assert.That((sx, sy), Is.EqualTo((389, 156)), "the shipped skiff golden, unmoved");
            Assert.That((px, py), Is.EqualTo((391, 210)));
            Assert.That((sx, sy), Is.Not.EqualTo((px, py)));

            HelmDashGeometry.WheelHub(ConsoleRigKind.Console, out sx, out sy);
            HelmDashGeometry.WheelHub(ConsoleRigKind.Novi, out px, out py);
            Assert.That((sx, sy), Is.EqualTo((300, 296)));
            Assert.That((px, py), Is.EqualTo((300, 382)));
            Assert.That((sx, sy), Is.Not.EqualTo((px, py)));

            HelmDashGeometry.DomeBoxOnCard(ConsoleRigKind.Console, out sx, out sy, out int sw, out int sh);
            HelmDashGeometry.DomeBoxOnCard(ConsoleRigKind.Novi, out px, out py, out int pw, out int ph);
            Assert.That((sx, sy, sw, sh), Is.EqualTo((308, -2, 124, 176)));
            Assert.That((px, py, pw, ph), Is.EqualTo((226, 4, 148, 182)));
            Assert.That((sx, sy, sw, sh), Is.Not.EqualTo((px, py, pw, ph)));

            // ...and the derived compass chain moves with the box, so the dome is not one baked picture.
            Assert.That(CompassRigRender.ComputeDomeLayout(308, -42, 124, 176).Gr, Is.EqualTo(52));
            Assert.That(CompassRigRender.ComputeDomeLayout(226, -50, 148, 182).Gr, Is.EqualTo(62));
        }

        // ---- the empty slots (the S4 trap) --------------------------------------------------------------

        private static HelmFit Fit(ConsoleRigKind rig, SounderKind sounder, CompassMount compass,
                                   bool radar, bool gps)
            => new HelmFit(rig, sounder, compass, radar, gps);

        private static DrawSurface RenderPilothouse(ConsoleRigKind rig, in HelmFit fit)
        {
            var s = new DrawSurface(HelmDashGeometry.CanvasW(rig), HelmDashGeometry.CanvasH(rig));
            if (rig == ConsoleRigKind.Novi)
                NoviDashRender.Render(s, in fit, running: true, 0.5f, 1f);
            else
                CapeDashRender.Render(s, in fit, running: true, 0.5f, 1f);
            return s;
        }

        private static Color32 At(DrawSurface s, int x, int y) => s.Pixels[y * s.Width + x];

        /// <summary>The SHIPPED fit of both wheelhouse hulls (<c>NoviHelm.asset</c> /
        /// <c>CapeIslanderHelm.asset</c>): a depth sounder, no compass, no radar, no gps.</summary>
        private static HelmFit ShippedFit(ConsoleRigKind rig)
            => Fit(rig, SounderKind.Depth, CompassMount.None, radar: false, gps: false);

        [TestCase(ConsoleRigKind.Novi)]
        [TestCase(ConsoleRigKind.Cape)]
        public void WithNothingFitted_TheRadarAndGpsMounts_RenderAsDeliberatelyEmpty(ConsoleRigKind rig)
        {
            DrawSurface s = RenderPilothouse(rig, ShippedFit(rig));

            foreach (int slot in new[] { HelmDashGeometry.PilotRadarSlot, HelmDashGeometry.PilotGpsSlot })
            {
                HelmDashGeometry.SlotBoxOnCard(slot, slot == HelmDashGeometry.PilotRadarSlot,
                                               out int x, out int y, out int w, out int h);
                // Every pixel of the mount is PAINTED — a blanking cover, never a hole in the dash
                // (the trap: an unfillable slot must not read as damage or show the card through).
                for (int yy = y + 2; yy < y + h - 2; yy += 7)
                {
                    for (int xx = x + 2; xx < x + w - 2; xx += 7)
                    {
                        Assert.That(At(s, xx, yy).a, Is.EqualTo(255),
                                    $"{rig} slot {slot} has a transparent pixel at ({xx},{yy})");
                    }
                }
            }
        }

        [TestCase(ConsoleRigKind.Novi)]
        [TestCase(ConsoleRigKind.Cape)]
        public void TheEmptyMount_LooksDifferentFromAFittedOne(ConsoleRigKind rig)
        {
            // If `fitted` were ignored, an unfittable slot would silently draw the standby page — the
            // dash would claim an instrument the boat has not got. Prove the two states differ.
            DrawSurface empty = RenderPilothouse(rig, ShippedFit(rig));
            DrawSurface fitted = RenderPilothouse(rig, Fit(rig, SounderKind.Depth, CompassMount.None,
                                                           radar: true, gps: true));
            HelmDashGeometry.SlotBoxOnCard(HelmDashGeometry.PilotGpsSlot, false,
                                           out int x, out int y, out int w, out int h);
            bool differs = false;
            for (int yy = y; yy < y + h && !differs; yy++)
                for (int xx = x; xx < x + w && !differs; xx++)
                {
                    Color32 a = At(empty, xx, yy), b = At(fitted, xx, yy);
                    differs = a.r != b.r || a.g != b.g || a.b != b.b;
                }
            Assert.That(differs, Is.True, $"{rig}: the blanked gps mount must not equal the fitted one");
        }

        [Test]
        public void EachWheelhouse_BlanksInItsOwnIdiom()
        {
            // The Cape screws a CORK plate over the cutout (capeRig.js:315, CORK[3] = #c0b082); the
            // Novi fits black glass (noviRig.js:331, GLASS = #050c0f). Same slot, same state, two
            // materials — sampled at the mount's centre-left, clear of the label and the screws.
            HelmDashGeometry.SlotBoxOnCard(HelmDashGeometry.PilotGpsSlot, false,
                                           out int x, out int y, out int w, out int h);
            int sx = x + 16, sy = y + h / 2;

            Color32 cape = At(RenderPilothouse(ConsoleRigKind.Cape, ShippedFit(ConsoleRigKind.Cape)), sx, sy);
            Color32 novi = At(RenderPilothouse(ConsoleRigKind.Novi, ShippedFit(ConsoleRigKind.Novi)), sx, sy);
            Assert.That(cape.r, Is.GreaterThan(120), "the Cape's blanking plate is buff cork, not glass");
            Assert.That(novi.r, Is.LessThan(60), "the Novi's blanking screen is black glass, not cork");
        }

        // ---- the standby page is a PARKED instrument, not a scanning one -------------------------------

        [TestCase(ConsoleRigKind.Novi)]
        [TestCase(ConsoleRigKind.Cape)]
        public void TheRadarStandbyPage_ParksItsSweepAtTwelveOClock(ConsoleRigKind rig)
        {
            // Merge-seat ruling (lead-architect, PR #410): a statically-labelled STANDBY screen is
            // honest — it announces it is producing no knowledge — but a ROTATING sweep would not be,
            // because a sweep implies scanning. The distinction is one animation frame wide, so it is
            // pinned geometrically rather than trusted: the sweep must lie on the mount's vertical
            // centre line (dir(0) = straight up), and nowhere else.
            DrawSurface s = RenderPilothouse(rig, Fit(rig, SounderKind.Depth, CompassMount.None,
                                                      radar: true, gps: false));
            HelmDashGeometry.SlotBoxOnCard(HelmDashGeometry.PilotRadarSlot, true,
                                           out int x, out int y, out int w, out int h);
            int cx = x + w / 2, cy = y + h / 2 + 6;              // the sweep's origin (js: cy + 6)

            // Sample at radius 55: past the middle ring (48) and short of the sweep's tip (h*0.42 = 63),
            // so ONLY the sweep can brighten a pixel there.
            const double r = 55.0;
            int Bright(double deg)
            {
                RigDrawUtil.Dir(deg * System.Math.PI / 180.0, out double dx, out double dy);
                Color32 c = At(s, DrawSurface.JsRound(cx + dx * r), DrawSurface.JsRound(cy + dy * r));
                return c.r + c.g + c.b;
            }

            int up = Bright(0), right = Bright(90), diag = Bright(45), down = Bright(180);
            Assert.That(up, Is.GreaterThan(right + 60),
                        $"{rig}: the sweep must point UP at 12 o'clock, not to starboard");
            Assert.That(up, Is.GreaterThan(diag + 60), $"{rig}: nor at 45°");
            Assert.That(up, Is.GreaterThan(down + 60), $"{rig}: nor astern");
        }

        [TestCase(ConsoleRigKind.Novi)]
        [TestCase(ConsoleRigKind.Cape)]
        public void TheDashChrome_IsPurelyAFunctionOfItsInputs(ConsoleRigKind rig)
        {
            // No clock, no frame counter, no hidden randomness anywhere in the chrome (rule 5) — which
            // is also what says the standby page cannot animate: two renders of one fit are the same
            // bytes. Covers the Cape's 1250 hash-placed cork motes and 40 grain streaks too.
            HelmFit fit = Fit(rig, SounderKind.Depth, CompassMount.None, radar: true, gps: true);
            DrawSurface a = RenderPilothouse(rig, fit);
            DrawSurface b = RenderPilothouse(rig, fit);
            for (int i = 0; i < a.Pixels.Length; i++)
            {
                Color32 p = a.Pixels[i], q = b.Pixels[i];
                if (p.r != q.r || p.g != q.g || p.b != q.b || p.a != q.a)
                    Assert.Fail($"{rig}: repaint differs at pixel {i} ({i % a.Width},{i / a.Width}) — " +
                                "something in the chrome is reading a clock or a random source");
            }
        }

        [TestCase(ConsoleRigKind.Novi)]
        [TestCase(ConsoleRigKind.Cape)]
        public void TheSounderMount_GrowsTallForTheColourFinder(ConsoleRigKind rig)
        {
            // S3b's sounder↔finder handover reaches these hulls: the fish finder is PORTRAIT and rises
            // into the headroom, so the mount the chrome draws has to change shape with the fit.
            DrawSurface depth = RenderPilothouse(rig, ShippedFit(rig));
            DrawSurface fish = RenderPilothouse(rig, Fit(rig, SounderKind.Fish, CompassMount.None,
                                                         false, false));
            HelmDashGeometry.SlotBoxOnCard(HelmDashGeometry.PilotSounderSlot, true,
                                           out int px, out int py, out _, out _);
            // A point just inside the PORTRAIT box's top edge is bare card under the landscape fit and
            // painted bezel under the portrait one.
            int probeX = px + 40, probeY = py + 1;
            Assert.That(At(depth, probeX, probeY).a, Is.EqualTo(0),
                        "the landscape sounder mount does not reach the headroom");
            Assert.That(At(fish, probeX, probeY).a, Is.Not.EqualTo(0),
                        "the portrait finder mount does");
        }
    }
}
