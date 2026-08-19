using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.UI.EditMode
{
    /// <summary>
    /// <b>Where the interaction popup sits, and what fits in it</b> — the two things about the owner's
    /// 2026-08-19 popup that can be got wrong in a way a headless test can catch.
    ///
    /// <para>Same discipline as <c>HudBandLayoutTests</c>: pure maths on
    /// <see cref="InteractPopupLayout"/>, no canvas anywhere. The LOOK is the coordinator's eyeball and
    /// the taste verdict is the owner's walk — this suite is only here so neither of them has to discover
    /// that the panel is off the bottom of the screen.</para>
    /// </summary>
    public class InteractPopupLayoutTests
    {
        // The host is a top-left-pivoted 1280×720 rectangle: x grows right, y grows DOWN (negative).
        private static Rect PanelRectInHost()
        {
            Vector2 p = InteractPopupLayout.PanelAnchoredPosition();
            Vector2 s = InteractPopupLayout.PanelSize();
            return new Rect(p.x, -p.y, s.x, s.y);   // flip y so the rect reads in ordinary screen terms
        }

        // =====================================================================================
        //  LOWER RIGHT, AND INSIDE THE SCREEN
        // =====================================================================================

        [Test]
        public void ThePanelIsInTheLOWERRIGHT()
        {
            Rect r = PanelRectInHost();
            Assert.Greater(r.center.x, InteractPopupLayout.RefW * 0.5f, "right of centre");
            Assert.Greater(r.center.y, InteractPopupLayout.RefH * 0.5f, "below centre (y grows down)");
        }

        [Test]
        public void ThePanelLiesEntirelyInsideTheReferenceScreen()
        {
            Rect r = PanelRectInHost();
            Assert.GreaterOrEqual(r.xMin, 0f);
            Assert.GreaterOrEqual(r.yMin, 0f);
            Assert.LessOrEqual(r.xMax, InteractPopupLayout.RefW);
            Assert.LessOrEqual(r.yMax, InteractPopupLayout.RefH);
        }

        [Test]
        public void ThePanelHugsBothEdgesByExactlyTheStatedMargin()
        {
            // Not "roughly in the corner": the margins are named so the popup's right edge lines up with
            // the HUD band's above it, and a drift there is visible at a glance in the build.
            Rect r = PanelRectInHost();
            Assert.AreEqual(InteractPopupLayout.MarginX, InteractPopupLayout.RefW - r.xMax, 1e-4f);
            Assert.AreEqual(InteractPopupLayout.MarginY, InteractPopupLayout.RefH - r.yMax, 1e-4f);
        }

        [Test]
        public void ThePopupsRightEdgeAgreesWithTheHudBandsSidePadding()
        {
            // The band takes SidePaddingRef off its width, 16 a side. The popup's margin is that half-gap,
            // so the two right edges are one line down the screen rather than two.
            Assert.AreEqual(HudBandLayout.SidePaddingRef * 0.5f, InteractPopupLayout.MarginX, 1e-4f);
        }

        [Test]
        public void ThePopupDoesNotReachUpIntoTheHudBand()
        {
            // The band owns the top 220 reference units. The popup is a corner note; it must not be a
            // second band, and it must not collide with the one that exists.
            Rect r = PanelRectInHost();
            Assert.Greater(r.yMin, HudBandLayout.BandHeightRef);
        }

        [Test]
        public void ItIsOneLineHigh_NotAPanel()
        {
            // "A SMALL popup" is the ruling's word. A height that crept past two lines of its own type
            // would be a panel, and panels are what the four surfaces replaced.
            Assert.Less(InteractPopupLayout.PanelHeight, InteractPopupLayout.FontSize * 2.5f);
            Assert.Greater(InteractPopupLayout.PanelHeight, InteractPopupLayout.FontSize);
            Assert.Less(InteractPopupLayout.PanelWidth, InteractPopupLayout.RefW * 0.25f,
                        "a quarter of the screen is already generous for one line");
        }

        // =====================================================================================
        //  THE TYPE INSIDE IT
        // =====================================================================================

        [Test]
        public void TheTypeBoxIsInsetOnBothAxes_AndFitsThePanel()
        {
            Vector2 size = InteractPopupLayout.TextSize();
            Assert.AreEqual(InteractPopupLayout.PanelWidth - InteractPopupLayout.TextPadX * 2f, size.x, 1e-4f);
            Assert.AreEqual(InteractPopupLayout.PanelHeight - InteractPopupLayout.TextPadY * 2f, size.y, 1e-4f);
            Assert.Greater(size.x, 0f);
            Assert.Greater(size.y, 0f);
        }

        [Test]
        public void TheTypeIsPlacedInsideThePanel_InTheSameTopLeftFrame()
        {
            Vector2 p = InteractPopupLayout.TextAnchoredPosition();
            Assert.AreEqual(InteractPopupLayout.TextPadX, p.x, 1e-4f);
            Assert.AreEqual(-InteractPopupLayout.TextPadY, p.y, 1e-4f, "y grows DOWN from the corner");
        }

        [Test]
        public void TheSortOrderSitsAboveTheHudAndBelowThePaperAndTheShell()
        {
            // Above the band (100) and the market screens (205) — it names something in the world and must
            // be readable over them. Below the tide table (210) and the whole shell (300+) — while a page
            // is out or the game is paused there is no world to interact with.
            Assert.Greater(InteractPopupLayout.SortingOrder, 205);
            Assert.Less(InteractPopupLayout.SortingOrder, 210);
            Assert.Less(InteractPopupLayout.SortingOrder, PaperUi.TitleSortingOrder);
            Assert.Less(InteractPopupLayout.SortingOrder, PaperUi.PauseSortingOrder);
        }

        // =====================================================================================
        //  Fit — ONE LINE, ALWAYS
        // =====================================================================================

        [Test]
        public void AShortLabelIsReturnedUNCHANGED_AndWithoutAllocating()
        {
            // The common path runs every time the offer changes, so it must not build a string. Reference
            // equality is the assertion that says so.
            const string label = "Climb aboard";
            Assert.AreSame(label, InteractPopupLayout.Fit(label));
        }

        [Test]
        public void NullOrEmptyBecomesTheEmptyString_NeverNull()
        {
            Assert.AreEqual("", InteractPopupLayout.Fit(null));
            Assert.AreEqual("", InteractPopupLayout.Fit(""));
        }

        [Test]
        public void AnOverLongLabelIsCUT_NotWrapped_AndTheCutIsMarked()
        {
            string long_ = new string('x', InteractPopupLayout.MaxCharacters + 40);
            string fit = InteractPopupLayout.Fit(long_);
            Assert.AreEqual(InteractPopupLayout.MaxCharacters, fit.Length,
                            "the drawn width is bounded whatever arrives");
            Assert.IsTrue(fit.EndsWith(InteractPopupLayout.Ellipsis), "and the cut is visible, not silent");
        }

        [Test]
        public void ALabelExactlyAtTheCapIsLeftAlone()
        {
            string exact = new string('y', InteractPopupLayout.MaxCharacters);
            Assert.AreSame(exact, InteractPopupLayout.Fit(exact), "the cap is inclusive");
        }

        [Test]
        public void ALineBreakIsCollapsed_SoTheSurfaceStaysOneLine()
        {
            Assert.AreEqual("Talk Aunt Ginny", InteractPopupLayout.Fit("Talk\nAunt Ginny"));
            Assert.AreEqual("Talk Aunt Ginny", InteractPopupLayout.Fit("Talk\rAunt Ginny"));
        }

        [Test]
        public void ALongLabelWithABreakIsBothCollapsedAndCut()
        {
            string awkward = "Talk\n" + new string('z', InteractPopupLayout.MaxCharacters + 10);
            string fit = InteractPopupLayout.Fit(awkward);
            Assert.AreEqual(InteractPopupLayout.MaxCharacters, fit.Length);
            Assert.IsFalse(fit.Contains("\n"));
        }

    }
}
