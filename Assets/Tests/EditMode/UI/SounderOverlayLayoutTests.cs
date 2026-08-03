using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.UI.EditMode
{
    /// <summary>
    /// The sounder card's placement + pointer mapping (ADR 0025 S2): where the small and focused cards
    /// sit, and — the load-bearing part — that a click anywhere on the card maps to exactly the control
    /// it looks like it is on, at BOTH scales. If the hit geometry didn't scale with the card, the
    /// focused state (the whole reason focus exists) would be unclickable.
    /// </summary>
    public class SounderOverlayLayoutTests
    {
        private const float ScreenW = 1920f, ScreenH = 1080f;

        private static DepthSounderSettings Cfg() => DepthSounderSettings.Default;

        [Test]
        public void TheSmallCard_SitsInsetFromTheTopRight_AtCardScale()
        {
            DepthSounderSettings s = Cfg();
            Rect r = SounderOverlayLayout.CardRect(false, DepthRigRender.W, DepthRigRender.H, in s,
                                                   ScreenW, ScreenH);
            Assert.AreEqual(DepthRigRender.W * s.CardScale, r.width, 1e-4f);
            Assert.AreEqual(DepthRigRender.H * s.CardScale, r.height, 1e-4f);
            Assert.AreEqual(ScreenW - s.MarginX, r.xMax, 1e-4f, "inset from the RIGHT edge");
            Assert.AreEqual(ScreenH - s.MarginY, r.yMax, 1e-4f, "and from the TOP — it is a brow instrument");
            Assert.Greater(r.yMin, ScreenH * 0.5f, "which keeps it clear of the piloting control's card");
        }

        [Test]
        public void TheFocusedCard_IsCentredOnTheAnchor_AtFocusScale()
        {
            DepthSounderSettings s = Cfg();
            Rect r = SounderOverlayLayout.CardRect(true, DepthRigRender.W, DepthRigRender.H, in s,
                                                   ScreenW, ScreenH);
            Assert.AreEqual(DepthRigRender.W * s.FocusScale, r.width, 1e-4f);
            Assert.AreEqual(ScreenW * s.FocusCenterX01, r.center.x, 1e-4f);
            Assert.AreEqual(ScreenH * s.FocusCenterY01, r.center.y, 1e-4f);
            Assert.Greater(s.FocusScale, s.CardScale, "focusing has to make it BIGGER to be worth doing");
        }

        [Test]
        public void EachControl_IsHitAtItsOwnCentre_AndNowhereElse()
        {
            RigRect mount = DepthRigGeometry.StandaloneMount();
            DepthRigLayout l = DepthRigGeometry.Layout(mount.X, mount.Y, mount.W, mount.H);

            for (int i = 0; i < 3; i++)
            {
                RigRect b = l.Button(i);
                var centre = new Vector2(b.X + b.W * 0.5f, b.Y + b.H * 0.5f);
                Assert.AreEqual(i, SounderOverlayLayout.HitTest(centre), $"the centre of pusher {i}");
            }

            var glass = new Vector2(l.Lcd.X + l.Lcd.W * 0.5f, l.Lcd.Y + l.Lcd.H * 0.5f);
            Assert.AreEqual(SounderOverlayLayout.LcdHit, SounderOverlayLayout.HitTest(glass),
                "a tap on the glass is the night toggle");

            // The brand strip along the bottom is chrome, not a control.
            var brand = new Vector2(l.Brand.X + 4, l.Brand.Y + l.Brand.H * 0.5f);
            Assert.AreEqual(-1, SounderOverlayLayout.HitTest(brand));
            Assert.AreEqual(-1, SounderOverlayLayout.HitTest(new Vector2(-5, -5)), "off the sprite entirely");
        }

        [Test]
        public void AClickOnAPusher_MapsThroughAtBothScales()
        {
            DepthSounderSettings s = Cfg();
            RigRect mount = DepthRigGeometry.StandaloneMount();
            DepthRigLayout l = DepthRigGeometry.Layout(mount.X, mount.Y, mount.W, mount.H);
            RigRect target = l.Button1;                       // the alarm-UP pusher
            // Rig space is y-DOWN from the top-left; the screen is y-UP from the bottom-left.
            float u = (target.X + target.W * 0.5f) / DepthRigRender.W;
            float v = 1f - (target.Y + target.H * 0.5f) / DepthRigRender.H;

            foreach (bool focused in new[] { false, true })
            {
                Rect card = SounderOverlayLayout.CardRect(focused, DepthRigRender.W, DepthRigRender.H,
                                                          in s, ScreenW, ScreenH);
                var screen = new Vector2(card.xMin + u * card.width, card.yMin + v * card.height);
                Assert.IsTrue(card.Contains(screen), $"focused={focused}: the point is on the card");
                Assert.IsTrue(HelmOverlayLayout.ScreenToRig(screen, card, DepthRigRender.W,
                                                            DepthRigRender.H, out Vector2 rigPx));
                Assert.AreEqual(1, SounderOverlayLayout.HitTest(rigPx),
                    $"focused={focused}: the same pixel is the same pusher at any card size");
            }
        }

        [Test]
        public void TheGlassAndThePushers_NeverBothClaimAPoint()
        {
            RigRect mount = DepthRigGeometry.StandaloneMount();
            DepthRigLayout l = DepthRigGeometry.Layout(mount.X, mount.Y, mount.W, mount.H);
            for (int y = 0; y < DepthRigRender.H; y += 3)
                for (int x = 0; x < DepthRigRender.W; x += 3)
                {
                    int claims = 0;
                    for (int i = 0; i < 3; i++) if (l.Button(i).Contains(x, y)) claims++;
                    if (l.Lcd.Contains(x, y)) claims++;
                    Assert.LessOrEqual(claims, 1, $"({x},{y}) is claimed by more than one control");
                }
        }
    }
}
