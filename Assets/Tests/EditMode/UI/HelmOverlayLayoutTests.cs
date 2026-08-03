using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.UI.EditMode
{
    /// <summary>
    /// The helm overlay's pure layout + pointer maths (ADR 0025 S1): the small-card placement, the
    /// click-to-FOCUS enlargement (owner addition 2026-08-03 — hit geometry scales with the card),
    /// screen→rig mapping, the grip grab test and the tiller drag mapping. Also a render smoke for
    /// both ported rigs (they draw pixels, differ across states, and stay inside their cells).
    /// </summary>
    public class HelmOverlayLayoutTests
    {
        private static HelmOverlaySettings Cfg => HelmOverlaySettings.Default;

        private const float ScreenW = 1920f, ScreenH = 1080f;

        [Test]
        public void CardRect_Small_SitsBottomRight_AtSmallScale()
        {
            HelmOverlaySettings s = Cfg;
            Rect r = HelmOverlayLayout.CardRect(false, LeverRigGeometry.W, LeverRigGeometry.H,
                                                in s, ScreenW, ScreenH);
            Assert.That(r.width, Is.EqualTo(LeverRigGeometry.W * s.SmallScale).Within(1e-3f));
            Assert.That(r.height, Is.EqualTo(LeverRigGeometry.H * s.SmallScale).Within(1e-3f));
            Assert.That(r.xMax, Is.EqualTo(ScreenW - s.MarginX).Within(1e-3f), "right margin");
            Assert.That(r.yMin, Is.EqualTo(s.MarginY).Within(1e-3f), "bottom margin");
        }

        [Test]
        public void CardRect_Focused_CentresOnTheAnchor_AtFocusScale()
        {
            HelmOverlaySettings s = Cfg;
            Rect r = HelmOverlayLayout.CardRect(true, TillerRigRender.W, TillerRigRender.H,
                                                in s, ScreenW, ScreenH);
            Assert.That(r.width, Is.EqualTo(TillerRigRender.W * s.FocusScale).Within(1e-3f),
                        "the enlargement IS the focus state — controls become properly clickable");
            Assert.That(r.center.x, Is.EqualTo(ScreenW * s.FocusCenterX01).Within(1e-3f));
            Assert.That(r.center.y, Is.EqualTo(ScreenH * s.FocusCenterY01).Within(1e-3f));
        }

        [Test]
        public void ScreenToRig_MapsCorners_WithTheYFlip_AndScalesWithFocus()
        {
            HelmOverlaySettings s = Cfg;
            Rect card = HelmOverlayLayout.CardRect(true, LeverRigGeometry.W, LeverRigGeometry.H,
                                                   in s, ScreenW, ScreenH);
            // Top-left of the card on screen = rig (0,0) — rig space is y-down.
            Assert.That(HelmOverlayLayout.ScreenToRig(new Vector2(card.xMin, card.yMax), card,
                        LeverRigGeometry.W, LeverRigGeometry.H, out Vector2 rig), Is.True);
            Assert.That(rig.x, Is.EqualTo(0f).Within(1e-3f));
            Assert.That(rig.y, Is.EqualTo(0f).Within(1e-3f));
            // Bottom-right = rig (W,H), regardless of the focus scale (hit geometry scales along).
            HelmOverlayLayout.ScreenToRig(new Vector2(card.xMax, card.yMin), card,
                                          LeverRigGeometry.W, LeverRigGeometry.H, out rig);
            Assert.That(rig.x, Is.EqualTo(LeverRigGeometry.W).Within(1e-3f));
            Assert.That(rig.y, Is.EqualTo(LeverRigGeometry.H).Within(1e-3f));
            // Outside → false.
            Assert.That(HelmOverlayLayout.ScreenToRig(new Vector2(card.xMin - 10f, card.yMin), card,
                        LeverRigGeometry.W, LeverRigGeometry.H, out _), Is.False);
        }

        [Test]
        public void IsOnGrip_TracksTheLiveGripPosition()
        {
            HelmOverlaySettings s = Cfg;
            LeverRigGeometry.HandleOffset(0.5, out double gx, out double gy);
            var onGrip = new Vector2((float)(LeverRigGeometry.PX + gx), (float)(LeverRigGeometry.PY + gy));
            Assert.That(HelmOverlayLayout.IsOnGrip(onGrip, 0.5f, s.GrabRadiusPx), Is.True);
            // The hub pivot is ~130 px from the grip at neutral-ish sigs — far outside the radius.
            var hub = new Vector2(LeverRigGeometry.PX, LeverRigGeometry.PY);
            Assert.That(HelmOverlayLayout.IsOnGrip(hub, 0.5f, s.GrabRadiusPx), Is.False);
        }

        [Test]
        public void LeverSigAt_ReadsTheSignalUnderThePointer()
        {
            LeverRigGeometry.HandleOffset(-0.5, out double gx, out double gy);
            float sig = HelmOverlayLayout.LeverSigAt(
                new Vector2((float)(LeverRigGeometry.PX + gx), (float)(LeverRigGeometry.PY + gy)));
            Assert.That(sig, Is.EqualTo(-0.5f).Within(1e-4f));
        }

        [Test]
        public void TillerDragDrive_UpIsAhead_ScaleInvariant_AndClamped()
        {
            HelmOverlaySettings s = Cfg;
            // A full-travel drag: TillerDragFullDrivePx rig px sweeps the whole −1..+1.
            float d = HelmOverlayLayout.TillerDragDrive(-1f, s.TillerDragFullDrivePx * 1f, 1f,
                                                        s.TillerDragFullDrivePx);
            Assert.That(d, Is.EqualTo(1f).Within(1e-4f), "full sweep = full range");
            // Focus scale doubles the screen delta for the same rig travel.
            float dFocused = HelmOverlayLayout.TillerDragDrive(-1f, s.TillerDragFullDrivePx * 2f, 2f,
                                                               s.TillerDragFullDrivePx);
            Assert.That(dFocused, Is.EqualTo(d).Within(1e-4f), "same felt travel in the focused state");
            Assert.That(HelmOverlayLayout.TillerDragDrive(0.9f, 10000f, 1f, s.TillerDragFullDrivePx),
                        Is.EqualTo(1f), "clamped");
            Assert.That(HelmOverlayLayout.TillerDragDrive(0f, -30f, 1f, s.TillerDragFullDrivePx),
                        Is.LessThan(0f), "down is astern");
        }

        // ---- render smoke: the ports draw, differ across states, and stay in their cells -----------

        [Test]
        public void LeverRender_DrawsPixels_AndDiffersAcrossSigAndFinish()
        {
            var a = new DrawSurface(LeverRigRender.W, LeverRigRender.H);
            LeverRigRender.Render(a, 0f, HelmLeverFinish.Graphite);
            Assert.That(OpaqueCount(a), Is.GreaterThan(3000), "the neutral lever is a real picture");

            var b = new DrawSurface(LeverRigRender.W, LeverRigRender.H);
            LeverRigRender.Render(b, 1f, HelmLeverFinish.Graphite);
            Assert.That(DiffCount(a, b), Is.GreaterThan(500), "full ahead draws a different pose");

            var c = new DrawSurface(LeverRigRender.W, LeverRigRender.H);
            LeverRigRender.Render(c, 0f, HelmLeverFinish.Chrome);
            Assert.That(DiffCount(a, c), Is.GreaterThan(200), "chrome vs graphite recolours the arm");
        }

        [Test]
        public void TillerRender_DrawsPixels_AndTheBandAndRockerFollowTheDrive()
        {
            var idle = new DrawSurface(TillerRigRender.W, TillerRigRender.H);
            TillerRigRender.Render(idle, 0f, running: true, press: false, forward: true,
                                   warn: false, blink: false);
            Assert.That(OpaqueCount(idle), Is.GreaterThan(3000), "the tiller is a real picture");

            var open = new DrawSurface(TillerRigRender.W, TillerRigRender.H);
            TillerRigRender.Render(open, 1f, running: true, press: false, forward: true,
                                   warn: false, blink: false);
            Assert.That(DiffCount(idle, open), Is.GreaterThan(50), "wide-open lights the throttle band");

            var reverse = new DrawSurface(TillerRigRender.W, TillerRigRender.H);
            TillerRigRender.Render(reverse, 0f, running: true, press: false, forward: false,
                                   warn: false, blink: false);
            Assert.That(DiffCount(idle, reverse), Is.GreaterThan(20), "the F/R rocker flips sides");
        }

        private static int OpaqueCount(DrawSurface s)
        {
            int n = 0;
            for (int i = 0; i < s.Pixels.Length; i++)
                if (s.Pixels[i].a > 8) n++;
            return n;
        }

        private static int DiffCount(DrawSurface a, DrawSurface b)
        {
            int n = 0;
            for (int i = 0; i < a.Pixels.Length; i++)
            {
                Color32 p = a.Pixels[i], q = b.Pixels[i];
                if (p.r != q.r || p.g != q.g || p.b != q.b || p.a != q.a) n++;
            }
            return n;
        }
    }
}
