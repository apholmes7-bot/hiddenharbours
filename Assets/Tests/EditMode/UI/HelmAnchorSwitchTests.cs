using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.UI.EditMode
{
    /// <summary>
    /// <b>The helm's anchor switch</b> — the diegetic ground-tackle control (ADR 0039), in both helm
    /// families and without a canvas:
    ///
    /// <list type="bullet">
    ///   <item><b>Where it sits.</b> The skiffs' third switch bat is DERIVED from the rig's own panel
    ///   numbers rather than measured, so this pins the derivation: same bat, same row, same tell-tale
    ///   line as DECK/SPOT, centred on the panel's own centreline — and clear of both of them and of
    ///   the ignition key. The pilothouse's is the rigs' already-authored <c>ANCH</c> breaker, so this
    ///   pins that it really is the fourth rocker of the port column and nothing has slid.</item>
    ///   <item><b>A press on it flicks the tackle</b> — one <c>ToggleAnchor</c>, through the Core seam,
    ///   with no drag session left open — and a press anywhere else does not.</item>
    ///   <item><b>A hull with no hook has no switch</b>, and pressing where one would be does nothing:
    ///   the pilothouse draws its ANCH breaker either way (it is authored panel furniture), so the
    ///   press has to be gated on the DATA and not only on the box.</item>
    ///   <item><b>The repaint key</b> follows the tackle: the chrome rebakes when the hook goes over
    ///   the side and then costs nothing again (rule 7).</item>
    /// </list>
    /// </summary>
    public class HelmAnchorSwitchTests
    {
        private static HelmOverlaySettings Cfg => HelmOverlaySettings.Default;

        // ---- doubles -------------------------------------------------------------------------------

        /// <summary>A helm that records what the dash asked of it. Everything but the ground tackle is
        /// parked: these tests are about the switch.</summary>
        private sealed class SwitchHelm : IHelmControl
        {
            public int Toggles;
            public int DriveWrites;
            public int SteerSessions;

            public bool HasHelm => true;
            public bool IsPlayerHelm => true;
            public HelmControlStyle Style => HelmControlStyle.Lever;
            public HelmLeverFinish LeverFinish => HelmLeverFinish.Graphite;
            public HelmWheelRim WheelRim => HelmWheelRim.Rubber;
            public HelmFit Fit { get; set; }
            public float Drive => 0f;
            public float Steer => 0f;

            public bool HasAnchor { get; set; } = true;
            public AnchorState AnchorState { get; set; } = AnchorState.Stowed;
            public void ToggleAnchor() => Toggles++;

            public void StepAhead() { }
            public void StepAstern() { }
            public void SetNeutral() { }
            public void DragDrive(float sig) => DriveWrites++;
            public void EndDrag() { }
            public bool SteerDragActive => false;
            public void DragSteer(float steer) => SteerSessions++;
            public void EndSteerDrag() { }
        }

        private static Vector2 CentreOfSwitch(ConsoleRigKind rig)
        {
            Assert.IsTrue(HelmDashGeometry.TryAnchorSwitchBox(rig, out int x, out int y, out int w, out int h),
                $"{rig} must offer an anchor switch box");
            return new Vector2(x + w / 2f, y + h / 2f);
        }

        // ---- 1. where it sits ----------------------------------------------------------------------

        [Test]
        public void TheSkiffBat_IsDerivedFromTheRigsOwnSwitchPanel_NotMeasured()
        {
            // Every number is a function of consoleRig.js:157-162 (== sportRig.js), which is what makes
            // this an extension of the rig rather than a second opinion about it.
            Assert.That(HelmDashGeometry.AnchCx,
                Is.EqualTo((HelmDashGeometry.DeckCx + HelmDashGeometry.SpotCx) / 2),
                "the third bat sits midway between the two it joins");
            Assert.That(HelmDashGeometry.AnchCx, Is.EqualTo(98));
            // The two independent derivations agree to a pixel — the panel's own centreline and the
            // ignition key both sit at 97. That agreement is the evidence the position is the real one
            // and not a number that happened to fit.
            Assert.That(Mathf.Abs(HelmDashGeometry.AnchCx - (HelmDashGeometry.SwX + HelmDashGeometry.SwW / 2)),
                Is.LessThanOrEqualTo(1), "…which is also the switch panel's centreline");
            Assert.That(Mathf.Abs(HelmDashGeometry.AnchCx - HelmDashGeometry.StartCx),
                Is.LessThanOrEqualTo(1), "…and the ignition key's");
            Assert.That(HelmDashGeometry.AnchW, Is.EqualTo(HelmDashGeometry.DeckW), "the same bat");
            Assert.That(HelmDashGeometry.AnchH, Is.EqualTo(HelmDashGeometry.DeckH));
            Assert.That(HelmDashGeometry.AnchY, Is.EqualTo(HelmDashGeometry.DeckY), "the same row");
            Assert.That(HelmDashGeometry.AnchLampY, Is.EqualTo(HelmDashGeometry.DeckLampY),
                "…and the same tell-tale line");
            Assert.That(HelmDashGeometry.AnchX,
                Is.EqualTo(HelmDashGeometry.AnchCx - HelmDashGeometry.AnchW / 2));
        }

        [Test]
        public void TheSkiffBat_FitsBetweenTheTwoLights_InsideThePanel_AndClearOfTheKey()
        {
            int l = HelmDashGeometry.DeckX + HelmDashGeometry.DeckW;   // DECK's right edge, 80
            int r = HelmDashGeometry.SpotX;                            // SPOT's left edge, 116
            Assert.Greater(HelmDashGeometry.AnchX, l, "the ANCH bat must not overlap the DECK bat");
            Assert.Less(HelmDashGeometry.AnchX + HelmDashGeometry.AnchW, r, "…nor the SPOT bat");

            // Evenly spaced: the three bats land at 69 / 98 / 127, so the gap either side of the new
            // one is the same 7 px — the reason the derivation lands where a hand-placed switch would.
            int gapLeft = HelmDashGeometry.AnchX - l;
            int gapRight = r - (HelmDashGeometry.AnchX + HelmDashGeometry.AnchW);
            Assert.That(gapLeft, Is.EqualTo(gapRight), "evenly spaced inside the row");
            Assert.Greater(gapLeft, 0, "…with real air either side, not a shared edge");

            // Inside the panel, and below the ignition key's circle (the key's bow reaches UP from cy).
            Assert.GreaterOrEqual(HelmDashGeometry.AnchX, HelmDashGeometry.SwX);
            Assert.LessOrEqual(HelmDashGeometry.AnchLampY + 10, HelmDashGeometry.SwY + HelmDashGeometry.SwH);
            Assert.Greater(HelmDashGeometry.AnchY,
                           HelmDashGeometry.StartCy + HelmDashGeometry.StartR,
                           "the bat starts below the ignition key, never under it");
        }

        [Test]
        public void ThePilothouseSwitch_IsTheRigsOwnAuthoredANCHBreaker()
        {
            // noviRig.js == capeRig.js: ColA = { DECK, BILGE, NAV, ANCH, PUMP, HORN }. The windlass
            // switch is that fourth rocker, wired — not a new control drawn over the bank.
            Assert.That(HelmDashGeometry.PilotAnchorRow, Is.EqualTo(3));
            Assert.That(HelmDashGeometry.PilotAnchorCol, Is.EqualTo(0), "the PORT column");
            Assert.Less(HelmDashGeometry.PilotAnchorRow, HelmDashGeometry.PilotRockRows);

            HelmDashGeometry.PilotRockerBox(HelmDashGeometry.PilotAnchorRow, HelmDashGeometry.PilotAnchorCol,
                                            out int x, out int y, out int w, out int h);
            Assert.That(x, Is.EqualTo(HelmDashGeometry.PilotRockColA));
            Assert.That(y, Is.EqualTo(HelmDashGeometry.PilotRockRow0
                                      + 3 * HelmDashGeometry.PilotRockDy + HelmDashGeometry.PilotTOPPAD));
            Assert.That(w, Is.EqualTo(HelmDashGeometry.PilotRockW));
            Assert.That(h, Is.EqualTo(HelmDashGeometry.PilotRockH));

            // …and it is genuinely inside the breaker bank it belongs to.
            Assert.GreaterOrEqual(x, HelmDashGeometry.PilotBankX);
            Assert.LessOrEqual(x + w, HelmDashGeometry.PilotBankX + HelmDashGeometry.PilotBankW);
            Assert.LessOrEqual(y + h,
                HelmDashGeometry.PilotBankY + HelmDashGeometry.PilotBankH + HelmDashGeometry.PilotTOPPAD);
        }

        [Test]
        public void EveryConsoledRig_OffersASwitchBox_AndAHullWithNoConsoleOffersNone()
        {
            foreach (ConsoleRigKind rig in new[] { ConsoleRigKind.Console, ConsoleRigKind.Sport,
                                                   ConsoleRigKind.Novi, ConsoleRigKind.Cape })
            {
                Assert.IsTrue(HelmDashGeometry.TryAnchorSwitchBox(rig, out int x, out int y, out int w, out int h),
                    $"{rig} draws a dash, so it has somewhere to put the switch");
                Assert.Greater(w, 0);
                Assert.Greater(h, 0);
                Assert.GreaterOrEqual(x, 0);
                Assert.GreaterOrEqual(y, 0);
                Assert.LessOrEqual(x + w, HelmDashGeometry.CanvasW(rig), $"{rig}: switch off the card");
                Assert.LessOrEqual(y + h, HelmDashGeometry.CanvasH(rig), $"{rig}: switch off the card");
            }

            // The rowed dory: no console, so no box at all rather than a defaulted one. Her tackle
            // answers to the anchor key instead — the whole reason that key still exists.
            Assert.IsFalse(HelmDashGeometry.TryAnchorSwitchBox(ConsoleRigKind.None, out _, out _, out _, out _));
            Assert.IsFalse(HelmDashGeometry.IsOnAnchorSwitch(ConsoleRigKind.None, new Vector2(97, 402)));
        }

        [Test]
        public void TheHitBox_TakesItsOwnSwitch_AndNothingElse()
        {
            foreach (ConsoleRigKind rig in new[] { ConsoleRigKind.Console, ConsoleRigKind.Sport,
                                                   ConsoleRigKind.Novi, ConsoleRigKind.Cape })
            {
                Assert.IsTrue(HelmDashGeometry.IsOnAnchorSwitch(rig, CentreOfSwitch(rig)), $"{rig}: centre");

                HelmDashGeometry.TryAnchorSwitchBox(rig, out int x, out int y, out int w, out int h);
                Assert.IsFalse(HelmDashGeometry.IsOnAnchorSwitch(rig, new Vector2(x - 2, y + h / 2f)),
                    $"{rig}: two pixels to port is not the switch");
                Assert.IsFalse(HelmDashGeometry.IsOnAnchorSwitch(rig, new Vector2(x + w + 2, y + h / 2f)));
                Assert.IsFalse(HelmDashGeometry.IsOnAnchorSwitch(rig, new Vector2(x + w / 2f, y - 2)));
                Assert.IsFalse(HelmDashGeometry.IsOnAnchorSwitch(rig, new Vector2(x + w / 2f, y + h + 2)));

                // The wheel is the control it could most plausibly be stolen from / by, and its grab
                // surface is a RADIUS with an owner-tunable pad — so this is the pair that has to be
                // measured rather than assumed, in both directions.
                HelmDashGeometry.WheelHub(rig, out int hx, out int hy);
                Assert.IsFalse(HelmDashGeometry.IsOnAnchorSwitch(rig, new Vector2(hx, hy)),
                    $"{rig}: the wheel hub is not the anchor switch");
                Assert.IsFalse(HelmDashGeometry.IsOnWheel(rig, CentreOfSwitch(rig),
                                                          HelmWheelSettings.Default.RimGrabPadPx),
                    $"{rig}: the switch sits clear of the wheel's grab circle at the shipped pad");
            }

            // On the skiffs it must also not eat the two working lights (the whole reason it needed a
            // position of its own rather than borrowing one).
            foreach (ConsoleRigKind rig in new[] { ConsoleRigKind.Console, ConsoleRigKind.Sport })
            {
                float y = HelmDashGeometry.DeckY + HelmDashGeometry.DeckH / 2f + HelmDashGeometry.TOPPAD;
                Assert.IsFalse(HelmDashGeometry.IsOnAnchorSwitch(rig, new Vector2(HelmDashGeometry.DeckCx, y)),
                    $"{rig}: the DECK bat is not the anchor switch");
                Assert.IsFalse(HelmDashGeometry.IsOnAnchorSwitch(rig, new Vector2(HelmDashGeometry.SpotCx, y)),
                    $"{rig}: nor the SPOT bat");
            }
        }

        // ---- 2. the press ---------------------------------------------------------------------------

        private static HelmDashController DashOn(ConsoleRigKind rig, SwitchHelm helm, out Texture2D tex)
        {
            var ctl = new HelmDashController();
            helm.Fit = new HelmFit(rig, SounderKind.None, CompassMount.None, false, false);
            tex = null;
            var go = new GameObject("anchor-switch-card");
            var image = go.AddComponent<UnityEngine.UI.RawImage>();
            // One paint so the controller knows which rig it is hit-testing against (that is what
            // UpdateAndPaint sets), exactly as the host drives it every frame.
            ctl.UpdateAndPaint(helm, helm.Fit, 0f, 0.016f, true, ref tex, image);
            Object.DestroyImmediate(go);
            return ctl;
        }

        [Test]
        public void APressOnTheSwitch_FlicksTheTackleOnce_AndOpensNoDragSession()
        {
            foreach (ConsoleRigKind rig in new[] { ConsoleRigKind.Console, ConsoleRigKind.Sport,
                                                   ConsoleRigKind.Novi, ConsoleRigKind.Cape })
            {
                var helm = new SwitchHelm();
                HelmOverlaySettings cfg = Cfg;
                HelmDashController ctl = DashOn(rig, helm, out Texture2D tex);

                ctl.Press(helm, CentreOfSwitch(rig), in cfg);

                Assert.That(helm.Toggles, Is.EqualTo(1), $"{rig}: one flick, one toggle");
                Assert.That(helm.DriveWrites, Is.EqualTo(0), $"{rig}: a switch is not the lever");
                Assert.That(helm.SteerSessions, Is.EqualTo(0), $"{rig}: nor the wheel");
                Assert.IsFalse(ctl.Interacting,
                    $"{rig}: a switch is a discrete flick — it must leave no drag session behind");

                if (tex != null) Object.DestroyImmediate(tex);
            }
        }

        [Test]
        public void AHullWithNoHook_HasNoSwitchDrawn_AndThePressDoesNothing()
        {
            // The pilothouse is the case that matters: its ANCH breaker is authored panel furniture and
            // is drawn whether or not this boat has tackle, so the box alone would happily take a press
            // and reach for an anchor that is not there.
            var helm = new SwitchHelm { HasAnchor = false };
            HelmOverlaySettings cfg = Cfg;
            HelmDashController ctl = DashOn(ConsoleRigKind.Novi, helm, out Texture2D tex);

            Assert.IsFalse(ctl.AnchorSwitchShown, "nothing to switch");
            ctl.Press(helm, CentreOfSwitch(ConsoleRigKind.Novi), in cfg);
            Assert.That(helm.Toggles, Is.EqualTo(0), "no hook, no flick");

            if (tex != null) Object.DestroyImmediate(tex);
        }

        // ---- 3. the repaint key ---------------------------------------------------------------------

        [Test]
        public void TheChrome_RebakesWhenTheHookGoesOverTheSide_AndThenCostsNothing()
        {
            var helm = new SwitchHelm();
            var go = new GameObject("anchor-switch-card");
            var image = go.AddComponent<UnityEngine.UI.RawImage>();
            Texture2D tex = null;
            try
            {
                var ctl = new HelmDashController();
                helm.Fit = new HelmFit(ConsoleRigKind.Console, SounderKind.None, CompassMount.None, false, false);

                Assert.IsTrue(ctl.UpdateAndPaint(helm, helm.Fit, 0f, 0.016f, false, ref tex, image),
                    "the first paint always draws");
                Assert.IsTrue(ctl.AnchorSwitchShown, "she carries a hook, so a switch is drawn");
                Assert.IsFalse(ctl.AnchorShown, "…stowed");

                Assert.IsFalse(ctl.UpdateAndPaint(helm, helm.Fit, 0f, 0.016f, false, ref tex, image),
                    "a steady dash repaints nothing (rule 7)");

                helm.AnchorState = AnchorState.Set;
                Assert.IsTrue(ctl.UpdateAndPaint(helm, helm.Fit, 0f, 0.016f, false, ref tex, image),
                    "the hook going down lights the switch, which is a chrome rebake");
                Assert.IsTrue(ctl.AnchorShown);
                Assert.IsFalse(ctl.UpdateAndPaint(helm, helm.Fit, 0f, 0.016f, false, ref tex, image),
                    "…and then costs nothing again while she lies to it");

                // Dragging is still DOWN as far as the switch is concerned — the drag cue is the red
                // rode and the toast, not a second switch state — so it must NOT keep repainting.
                helm.AnchorState = AnchorState.Dragging;
                Assert.IsFalse(ctl.UpdateAndPaint(helm, helm.Fit, 0f, 0.016f, false, ref tex, image),
                    "set → dragging is the same switch position; no rebake");

                helm.AnchorState = AnchorState.Stowed;
                Assert.IsTrue(ctl.UpdateAndPaint(helm, helm.Fit, 0f, 0.016f, false, ref tex, image),
                    "weighing her puts the switch back");
                Assert.IsFalse(ctl.AnchorShown);
            }
            finally
            {
                Object.DestroyImmediate(go);
                if (tex != null) Object.DestroyImmediate(tex);
            }
        }
    }
}
