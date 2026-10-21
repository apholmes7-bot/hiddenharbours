using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.UI.EditMode
{
    /// <summary>
    /// <b>Where a raised instrument card goes</b> (H2; the owner's Q1 answer, B, 2026-10-17), the one
    /// rule all four instrument hosts place their card by (<see cref="HelmBandLayout.RaisedCardRect"/>):
    /// the instrument's own native raster at the largest WHOLE scale that fits between the band's top
    /// and the top HUD band, centred there; and where no whole scale fits, today's scale, its top on
    /// the top band's bottom, reaching down over the band (the wheel included) while it is raised.
    /// Never into the top band. Pure: no canvas, no texture.
    /// </summary>
    public class HelmRaisedCardLayoutTests
    {
        /// <summary>The ruling's screens: 720p, 800p, 1080p, 1440p, ultrawide and 4K.</summary>
        private static readonly Vector2Int[] Screens =
        {
            new Vector2Int(1280, 720), new Vector2Int(1280, 800), new Vector2Int(1920, 1080),
            new Vector2Int(2560, 1440), new Vector2Int(3440, 1440), new Vector2Int(3840, 2160),
        };

        private struct Card
        {
            public string Name;
            public int W, H;
            public int[] Whole;   // the largest whole scale between the bands on each of Screens (0: none)
        }

        /// <summary>Phase A's table (its section 1.8), instrument by instrument.</summary>
        private static readonly Card[] Cards =
        {
            new Card { Name = "depth", W = DepthRigRender.W, H = DepthRigRender.H, Whole = new[] { 1, 1, 1, 2, 2, 3 } },
            new Card { Name = "finder", W = FishRigRender.W, H = FishRigRender.H, Whole = new[] { 0, 0, 0, 1, 1, 1 } },
            new Card { Name = "radar", W = RadarRigRender.W, H = RadarRigRender.H, Whole = new[] { 0, 0, 0, 1, 1, 1 } },
            new Card { Name = "plotter", W = NavRigGeometry.ConsoleW, H = NavRigGeometry.ConsoleH, Whole = new[] { 0, 0, 1, 1, 1, 2 } },
            new Card { Name = "plotter max", W = NavRigGeometry.MaxW, H = NavRigGeometry.MaxH, Whole = new[] { 0, 0, 0, 1, 1, 1 } },
        };

        private static float Reserve(float w, float h) => HudBandLayout.ReservedTopPx(w, h, 0f);

        private static Rect Raised(Card c, Vector2Int s) => HelmBandLayout.RaisedCardRect(c.W, c.H, s.x, s.y, Reserve(s.x, s.y));

        [Test]
        public void ARaisedCard_TakesTheLargestWholeScaleBetweenTheBands_AtEveryListedScreen()
        {
            Assert.AreEqual(new Vector2Int(480, 330), new Vector2Int(DepthRigRender.W, DepthRigRender.H), "premise: the sounder's native face");
            Assert.AreEqual(new Vector2Int(480, 660), new Vector2Int(FishRigRender.W, FishRigRender.H), "premise: the finder's");
            Assert.AreEqual(new Vector2Int(480, 660), new Vector2Int(RadarRigRender.W, RadarRigRender.H), "premise: the radar's");

            foreach (Card c in Cards)
            {
                for (int s = 0; s < Screens.Length; s++)
                {
                    Vector2Int sc = Screens[s];
                    string at = $"the {c.Name} at {sc.x}x{sc.y}";
                    float top = sc.y - Reserve(sc.x, sc.y);
                    float band = HelmBandLayout.BandHeightPx(sc.y);
                    int n = HelmBandLayout.RaisedCardWholeScale(c.W, c.H, sc.x, sc.y, Reserve(sc.x, sc.y));
                    Assert.AreEqual(c.Whole[s], n, at + ": the largest whole scale between the bands");
                    if (n == 0) continue;

                    Rect r = Raised(c, sc);
                    Assert.AreEqual(c.W * n, r.width, 1e-3f, at + ": the native raster at xn");
                    Assert.AreEqual(c.H * n, r.height, 1e-3f, at + ": …both axes");
                    Assert.AreEqual(Mathf.Floor((sc.x - r.width) / 2f), r.xMin, 1e-3f, at + ": centred across");
                    Assert.AreEqual((band + top) / 2f, r.center.y, 1f, at + ": centred between the bands");
                    Assert.GreaterOrEqual(r.yMin, band - 1e-3f, at + ": clear of the band");
                    Assert.LessOrEqual(r.yMax, top + 1e-3f, at + ": clear of the top band");
                    Assert.AreEqual((float)n, HelmBandLayout.RaisedCardScale(c.W, c.H, sc.x, sc.y, Reserve(sc.x, sc.y)), 1e-5f,
                        at + ": the scale the hosts map their pointer through");
                }
            }
        }

        [Test]
        public void WhereNoWholeScaleFits_ARaisedCard_KeepsTodaysScale_AndReachesOverTheBand()
        {
            // Today's scales (no bottom band: the fit below the top band alone), and how far down over
            // the band each card then reaches while raised (Q1: B).
            var cases = new[]
            {
                // name, screen, today's scale, the card's bottom edge
                ("finder", 0, 0.76f, 0f), ("finder", 1, 0.86f, 0f), ("finder", 2, 1f, 90f),
                ("radar", 0, 0.76f, 0f), ("radar", 1, 0.86f, 0f), ("radar", 2, 1f, 90f),
                ("plotter", 0, 1f, 20f), ("plotter", 1, 1f, 88.1f),
                ("plotter max", 0, 0.77f, 0f), ("plotter max", 1, 0.88f, 0f), ("plotter max", 2, 1f, 102f),
            };
            foreach ((string name, int s, float scale, float bottom) in cases)
            {
                Card c = System.Array.Find(Cards, x => x.Name == name);
                Vector2Int sc = Screens[s];
                string at = $"the {name} at {sc.x}x{sc.y}";
                Assert.AreEqual(0, c.Whole[s], "premise: " + at + " has no whole scale between the bands");

                Rect r = Raised(c, sc);
                float top = sc.y - Reserve(sc.x, sc.y);
                float band = HelmBandLayout.BandHeightPx(sc.y);
                Assert.AreEqual(scale, r.width / c.W, 0.005f, at + ": today's scale");
                Assert.AreEqual((double)c.W / c.H, (double)r.width / r.height, 1e-4, at + ": undistorted");
                Assert.AreEqual(top, r.yMax, 0.5f, at + ": its top on the top band's bottom");
                Assert.AreEqual(bottom, r.yMin, 0.5f, at + ": its bottom edge");
                Assert.Less(r.yMin, band, at + ": so it reaches down over the band while raised");
                Assert.AreEqual(sc.x / 2f, r.center.x, 1f, at + ": centred across");
                Assert.IsTrue(r.xMin < sc.x / 2f && sc.x / 2f < r.xMax, at + ": over the wheel, which keys still steer");
                Assert.AreEqual(scale, HelmBandLayout.RaisedCardScale(c.W, c.H, sc.x, sc.y, Reserve(sc.x, sc.y)), 0.005f,
                    at + ": the scale the hosts map their pointer through");
            }
        }

        [Test]
        public void ARaisedCard_NeverReachesIntoTheTopBand()
        {
            var screens = new[]
            {
                new Vector2Int(1024, 480), new Vector2Int(1024, 600), new Vector2Int(1280, 720), new Vector2Int(1280, 800),
                new Vector2Int(1366, 768), new Vector2Int(1600, 900), new Vector2Int(1920, 1080), new Vector2Int(1920, 1200),
                new Vector2Int(2560, 1080), new Vector2Int(2560, 1440), new Vector2Int(3440, 1440), new Vector2Int(3840, 2160),
                new Vector2Int(5120, 2880),
            };
            foreach (Card c in Cards)
            {
                foreach (Vector2Int sc in screens)
                {
                    foreach (float inset in new[] { 0f, 48f })
                    {
                        string at = $"the {c.Name} at {sc.x}x{sc.y}, inset {inset}";
                        float reserve = HudBandLayout.ReservedTopPx(sc.x, sc.y, inset);
                        Rect r = HelmBandLayout.RaisedCardRect(c.W, c.H, sc.x, sc.y, reserve);
                        Assert.Greater(r.width, 0f, at + ": room for a card");
                        Assert.LessOrEqual(r.yMax, sc.y - reserve + 1e-3f, at + ": never into the top band");
                        Assert.GreaterOrEqual(r.yMin, -1e-3f, at + ": nor off the bottom of the screen");
                        Assert.GreaterOrEqual(r.xMin, -1e-3f, at + ": nor off its left");
                        Assert.LessOrEqual(r.xMax, sc.x + 1e-3f, at + ": nor off its right");
                        Assert.AreEqual((double)c.W / c.H, (double)r.width / r.height, 1e-4, at + ": undistorted");
                        Assert.LessOrEqual(r.height, Mathf.Max(c.H, c.H * HelmBandLayout.RaisedCardWholeScale(c.W, c.H, sc.x, sc.y, reserve)) + 1e-3f,
                            at + ": never past native unless at a whole scale");
                    }
                }
            }
            Assert.AreEqual(Rect.zero, HelmBandLayout.RaisedCardRect(DepthRigRender.W, DepthRigRender.H, 1280f, 200f, 220f),
                "no room below the top band: no card");
            Assert.AreEqual(Rect.zero, HelmBandLayout.RaisedCardRect(0, DepthRigRender.H, 1280f, 720f, 220f), "no raster: no card");
        }
    }
}
