using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.UI.EditMode
{
    /// <summary>
    /// The S4.5 OVERLAP rule, from the other side: the always-on HUD band stays, so the helm overlays
    /// keep out from under it (<see cref="HudBandLayout"/>). Pure maths — no canvas, no screen.
    ///
    /// <para>The regression these pin is real and shipped: at 1920×1080 the focused dash reached
    /// screen y 781 against a band bottom of 750, and the band's canvas sorts at 100 against the
    /// helm's 60, so the clock and the tide were drawn across the top of the wheelhouse. At 1280×720
    /// the dash ran the full height and the whole band sat on it.</para>
    /// </summary>
    public class HudBandLayoutTests
    {
        // The two hull families' canvases (HelmDashGeometry) and the shipped overlay dials.
        private const int SkiffW = 600, SkiffH = 510, PilotW = 600, PilotH = 548;

        private static float Band(float w, float h) => HudBandLayout.ReservedTopPx(w, h, 0f);

        [Test]
        public void TheScaleFactorIsUnitysOwn()
        {
            // ScaleWithScreenSize, match 0.5 → the geometric mean of the two ratios.
            Assert.That(HudBandLayout.ScaleFactor(HudBandLayout.RefW, HudBandLayout.RefH),
                        Is.EqualTo(1f).Within(1e-5f), "at the reference resolution, 1:1");
            Assert.That(HudBandLayout.ScaleFactor(1920f, 1080f), Is.EqualTo(1.5f).Within(1e-4f));
            // A window twice as wide but the reference height: sqrt(2) × 1.
            Assert.That(HudBandLayout.ScaleFactor(2560f, 720f), Is.EqualTo(Mathf.Sqrt(2f)).Within(1e-4f));
            Assert.That(HudBandLayout.ScaleFactor(0f, 0f), Is.EqualTo(1f), "a degenerate screen is 1:1");
        }

        [Test]
        public void TheReserveIsTheBandsHeightPlusAnySafeArea()
        {
            Assert.That(Band(1920f, 1080f),
                        Is.EqualTo(HudBandLayout.BandHeightRef * 1.5f).Within(0.01f));
            Assert.That(HudBandLayout.ReservedTopPx(1920f, 1080f, 44f),
                        Is.EqualTo(HudBandLayout.BandHeightRef * 1.5f + 44f).Within(0.01f),
                        "a notch pushes the band down, so it pushes the reserve down too");
        }

        // ---- the dash ------------------------------------------------------------------------------

        [Test]
        public void REGRESSION_TheFocusedDashUsedToReachIntoTheBand()
        {
            // The negative control for the fix: run the OLD call (reserve 0) and prove it really did
            // overlap. If this ever stops overlapping, the fix below is no longer load-bearing and the
            // reserve should be re-derived rather than kept out of habit.
            HelmOverlaySettings s = HelmOverlaySettings.Default;
            Rect old = HelmOverlayLayout.DashCardRect(true, PilotW, PilotH, in s, 1920f, 1080f);
            float bandBottom = 1080f - Band(1920f, 1080f);
            Assert.That(old.yMax, Is.GreaterThan(bandBottom),
                        "SABOTAGE NOT DETECTED: the unreserved focused dash no longer reaches the " +
                        "band, so this fix is pinning nothing — re-derive against the current dials");

            Rect now = HelmOverlayLayout.DashCardRect(true, PilotW, PilotH, in s, 1920f, 1080f,
                                                      Band(1920f, 1080f));
            Assert.That(now.yMax, Is.LessThanOrEqualTo(bandBottom + 0.01f), "…and now it does not");
        }

        [Test]
        public void TheDashClearsTheBandAtEveryPlausibleWindow()
        {
            HelmOverlaySettings s = HelmOverlaySettings.Default;
            (float w, float h)[] screens =
            {
                (1280f, 720f), (1366f, 768f), (1600f, 900f), (1920f, 1080f), (2560f, 1440f),
                (3840f, 2160f), (1024f, 600f), (2560f, 1080f),
            };
            foreach (var (w, h) in screens)
            {
                float reserve = Band(w, h);
                foreach (bool focused in new[] { false, true })
                {
                    foreach (var (rw, rh) in new[] { (SkiffW, SkiffH), (PilotW, PilotH) })
                    {
                        Rect card = HelmOverlayLayout.DashCardRect(focused, rw, rh, in s, w, h, reserve);
                        Assert.That(card.yMax, Is.LessThanOrEqualTo(h - reserve + 0.01f),
                                    $"{w}x{h} focused={focused} rig={rw}x{rh}: the dash is under the band");
                        Assert.That(card.width, Is.GreaterThan(0f), $"{w}x{h}: the dash vanished");
                        Assert.That(card.width / card.height,
                                    Is.EqualTo(rw / (float)rh).Within(1e-3f), "never distorted");
                    }
                }
            }
        }

        [Test]
        public void TheSmallDashIsUntouchedByTheReserve()
        {
            // The reserve must bite on the focused state only. The small card is a quarter of the
            // screen at the bottom and nowhere near the band; if this changes, the owner's
            // DashSmallScale has quietly stopped meaning what it says.
            HelmOverlaySettings s = HelmOverlaySettings.Default;
            Rect free = HelmOverlayLayout.DashCardRect(false, PilotW, PilotH, in s, 1920f, 1080f);
            Rect held = HelmOverlayLayout.DashCardRect(false, PilotW, PilotH, in s, 1920f, 1080f,
                                                       Band(1920f, 1080f));
            Assert.That(held, Is.EqualTo(free));
        }

        [Test]
        public void TheOldOverloadStillMeansTheOldThing()
        {
            // Additive, not a behaviour change to the existing signature: reserve 0 is the shipped
            // maths exactly, so every S2a/S4 call site and golden keeps its meaning.
            HelmOverlaySettings s = HelmOverlaySettings.Default;
            foreach (bool focused in new[] { false, true })
                Assert.That(HelmOverlayLayout.DashCardRect(focused, SkiffW, SkiffH, in s, 1920f, 1080f),
                            Is.EqualTo(HelmOverlayLayout.DashCardRect(focused, SkiffW, SkiffH, in s,
                                                                      1920f, 1080f, 0f)));
        }

        // ---- an expanded instrument ------------------------------------------------------------------

        [Test]
        public void AnExpandedInstrumentSlidesDownBeforeItShrinks()
        {
            // The centred 2× sounder card (960×660 at 1080p) fits under the band — but only if it is
            // allowed to move off centre. Losing position beats losing size: this is the state the
            // instrument is actually read in.
            float reserve = Band(1920f, 1080f);
            var centred = new Rect(1920f * 0.5f - 480f, 1080f * 0.5f - 330f, 960f, 660f);
            Assert.That(centred.yMax, Is.GreaterThan(1080f - reserve), "the premise: it did overlap");

            Rect fitted = HudBandLayout.FitBelowBand(centred, 1920f, 1080f, reserve);
            Assert.That(fitted.width, Is.EqualTo(centred.width).Within(0.01f), "not shrunk…");
            Assert.That(fitted.height, Is.EqualTo(centred.height).Within(0.01f));
            Assert.That(fitted.yMax, Is.LessThanOrEqualTo(1080f - reserve + 0.01f), "…just moved down");
            Assert.That(fitted.yMin, Is.GreaterThanOrEqualTo(-0.01f), "and still on screen");
            Assert.That(fitted.center.x, Is.EqualTo(centred.center.x).Within(0.01f), "horizontally put");
        }

        [Test]
        public void AnExpandedInstrumentShrinksUndistortedWhenThereIsGenuinelyNoRoom()
        {
            // A short window: 660 px of portrait finder into 720 − 220 = 500 px of usable screen.
            float reserve = Band(1280f, 720f);
            var centred = new Rect(1280f * 0.5f - 240f, 720f * 0.5f - 330f, 480f, 660f);
            Rect fitted = HudBandLayout.FitBelowBand(centred, 1280f, 720f, reserve);

            Assert.That(fitted.height, Is.EqualTo(720f - reserve).Within(0.01f));
            Assert.That(fitted.width / fitted.height,
                        Is.EqualTo(centred.width / centred.height).Within(1e-3f),
                        "a portrait rig is never squashed to fit");
            Assert.That(fitted.yMax, Is.LessThanOrEqualTo(720f - reserve + 0.01f));
        }

        [Test]
        public void AlreadyClearMeansUntouched()
        {
            float reserve = Band(1920f, 1080f);
            var low = new Rect(600f, 20f, 400f, 300f);
            Assert.That(HudBandLayout.FitBelowBand(low, 1920f, 1080f, reserve), Is.EqualTo(low));
        }

        [Test]
        public void NoRoomAtAllDegradesToEmpty_NotInverted()
        {
            // A pathological reserve must not produce a negative-height rect that would render as a
            // flipped instrument; an empty one is honest.
            Rect r = HudBandLayout.FitBelowBand(new Rect(0f, 0f, 400f, 300f), 1920f, 1080f, 2000f);
            Assert.That(r.width, Is.EqualTo(0f));
            Assert.That(r.height, Is.EqualTo(0f));
        }
    }
}
