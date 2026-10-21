using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.UI.EditMode
{
    /// <summary>
    /// The sounder card's pointer mapping (ADR 0025 S2): the load-bearing part — that a click anywhere
    /// on the card maps to exactly the control it looks like it is on, at every scale the RAISED card
    /// takes (H2: the card rises from its housing in the band, <see cref="HelmBandLayout.RaisedCardRect"/>).
    /// If the hit geometry didn't scale with the card, a raised card would be unclickable. Where the
    /// small and focused cards sat retired with the focus (H2).
    /// </summary>
    public class SounderOverlayLayoutTests
    {
        /// <summary>Screens whose raised sounder card takes ×1, ×2 and ×3 between the bands, and one
        /// short enough that no whole scale fits (1024×480: today's scale, ×0.97, its top on the top
        /// band's bottom).</summary>
        private static readonly Vector2Int[] Screens =
        {
            new Vector2Int(1280, 720), new Vector2Int(2560, 1440), new Vector2Int(3840, 2160),
            new Vector2Int(1024, 480),
        };

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
            RigRect mount = DepthRigGeometry.StandaloneMount();
            DepthRigLayout l = DepthRigGeometry.Layout(mount.X, mount.Y, mount.W, mount.H);
            RigRect target = l.Button1;                       // the alarm-UP pusher
            // Rig space is y-DOWN from the top-left; the screen is y-UP from the bottom-left.
            float u = (target.X + target.W * 0.5f) / DepthRigRender.W;
            float v = 1f - (target.Y + target.H * 0.5f) / DepthRigRender.H;

            var scales = new System.Collections.Generic.HashSet<float>();
            foreach (Vector2Int sc in Screens)
            {
                Rect card = HelmBandLayout.RaisedCardRect(DepthRigRender.W, DepthRigRender.H, sc.x, sc.y,
                                                          HudBandLayout.ReservedTopPx(sc.x, sc.y, 0f));
                float scale = card.width / DepthRigRender.W;
                scales.Add(scale);
                var screen = new Vector2(card.xMin + u * card.width, card.yMin + v * card.height);
                Assert.IsTrue(card.Contains(screen), $"{sc} (x{scale:0.###}): the point is on the card");
                Assert.IsTrue(HelmOverlayLayout.ScreenToRig(screen, card, DepthRigRender.W,
                                                            DepthRigRender.H, out Vector2 rigPx));
                Assert.AreEqual(1, SounderOverlayLayout.HitTest(rigPx),
                    $"{sc} (x{scale:0.###}): the same pixel is the same pusher at any card size");
            }
            Assert.AreEqual(Screens.Length, scales.Count,
                "every listed screen raises the card at a scale of its own, or the sweep pins less than it says");
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
