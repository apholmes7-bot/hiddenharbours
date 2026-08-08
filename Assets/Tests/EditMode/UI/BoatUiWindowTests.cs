using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using HiddenHarbours.Core;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.UI.EditMode
{
    /// <summary>
    /// The boat-UI windowing model (the owner's 2026-08-07 ruling), pinned where it is pure:
    /// the state registry (collapse cycle, hide-all, the chrome publication, the one drag session)
    /// and the geometry (<see cref="BoatUiWindowLayout"/> — the shipped-layout identity, scaling,
    /// clamping, chrome hit boxes, drag maths). The canvas-and-pointer half is
    /// <c>BoatUiWindowPlayTests</c>.
    /// </summary>
    public class BoatUiWindowTests
    {
        private const string ConfigAssetPath = "Assets/_Project/Data/Config/GameConfig.asset";

        private static readonly BoatUiWindowSettings Cfg = BoatUiWindowSettings.Default;

        [SetUp]
        public void SetUp() => BoatUiWindows.Reset();

        [TearDown]
        public void TearDown() => BoatUiWindows.Reset();

        // ---- the registry -------------------------------------------------------------------------

        [Test]
        public void TheCollapseCycle_IsFullCompactBar_AndRoundAgain()
        {
            Assert.That(BoatUiWindows.NextCollapse(BoatUiCollapse.Full),
                        Is.EqualTo(BoatUiCollapse.Compact));
            Assert.That(BoatUiWindows.NextCollapse(BoatUiCollapse.Compact),
                        Is.EqualTo(BoatUiCollapse.Bar));
            Assert.That(BoatUiWindows.NextCollapse(BoatUiCollapse.Bar),
                        Is.EqualTo(BoatUiCollapse.Full));
        }

        [Test]
        public void HideAll_HidesEveryWindow_AndTheRestoreClearsIndividualHides()
        {
            BoatUiWindows.SetHidden(BoatUiWindowId.Watch, true);
            Assert.That(BoatUiWindows.IsShown(BoatUiWindowId.Watch), Is.False,
                        "an individual hide hides that window");
            Assert.That(BoatUiWindows.IsShown(BoatUiWindowId.Helm), Is.True,
                        "…and only that window");

            BoatUiWindows.ToggleHideAll();
            Assert.That(BoatUiWindows.AllHidden, Is.True);
            Assert.That(BoatUiWindows.IsShown(BoatUiWindowId.Helm), Is.False);
            Assert.That(BoatUiWindows.IsShown(BoatUiWindowId.Chartplotter), Is.False);

            BoatUiWindows.ToggleHideAll();
            Assert.That(BoatUiWindows.AllHidden, Is.False);
            Assert.That(BoatUiWindows.IsShown(BoatUiWindowId.Watch), Is.True,
                        "the RESTORE half clears individual hides too — it is the one guaranteed " +
                        "recovery path back to a window whose own chrome is off screen");
        }

        [Test]
        public void FlushMounts_FollowTheHelmWindow()
        {
            Assert.That(BoatUiWindows.FlushMountsAvailable, Is.True, "default: the dash carries its brow");

            BoatUiWindows.SetHidden(BoatUiWindowId.Helm, true);
            Assert.That(BoatUiWindows.FlushMountsAvailable, Is.False, "a hidden dash has no brow");
            BoatUiWindows.SetHidden(BoatUiWindowId.Helm, false);

            BoatUiWindowState st = BoatUiWindows.State(BoatUiWindowId.Helm);
            st.Collapse = BoatUiCollapse.Bar;
            BoatUiWindows.SetState(BoatUiWindowId.Helm, in st);
            Assert.That(BoatUiWindows.FlushMountsAvailable, Is.False,
                        "a bar-collapsed dash folds its brow away with it");

            st.Collapse = BoatUiCollapse.Compact;
            BoatUiWindows.SetState(BoatUiWindowId.Helm, in st);
            Assert.That(BoatUiWindows.FlushMountsAvailable, Is.True,
                        "Compact still draws the dash picture, so the mounts stand");
        }

        [Test]
        public void TheChromePublication_RoutesPresses_AndClears()
        {
            Assert.That(BoatUiWindows.OverAnyChrome(new Vector2(50f, 50f)), Is.False);

            BoatUiWindows.PublishChrome(BoatUiWindowId.Sounder, new Rect(40f, 40f, 100f, 18f));
            Assert.That(BoatUiWindows.OverAnyChrome(new Vector2(50f, 50f)), Is.True);
            Assert.That(BoatUiWindows.OverAnyChrome(new Vector2(200f, 50f)), Is.False);

            BoatUiWindows.ClearChrome(BoatUiWindowId.Sounder);
            Assert.That(BoatUiWindows.OverAnyChrome(new Vector2(50f, 50f)), Is.False,
                        "a withdrawn publication must stop swallowing clicks");
        }

        [Test]
        public void TheDragSession_IsSingleAndOwned()
        {
            Assert.That(BoatUiWindows.AnySession, Is.False);
            Assert.That(BoatUiWindows.BeginSession(BoatUiWindowId.Radar, BoatUiDragKind.Move), Is.True);
            Assert.That(BoatUiWindows.SessionKind(BoatUiWindowId.Radar), Is.EqualTo(BoatUiDragKind.Move));
            Assert.That(BoatUiWindows.SessionKind(BoatUiWindowId.Helm), Is.EqualTo(BoatUiDragKind.None));

            Assert.That(BoatUiWindows.BeginSession(BoatUiWindowId.Helm, BoatUiDragKind.Resize), Is.False,
                        "one pointer, one session — a second window cannot steal a live drag");

            BoatUiWindows.EndSession(BoatUiWindowId.Helm);
            Assert.That(BoatUiWindows.AnySession, Is.True, "only the owner can end its session");
            BoatUiWindows.EndSession(BoatUiWindowId.Radar);
            Assert.That(BoatUiWindows.AnySession, Is.False);
        }

        // ---- the geometry -------------------------------------------------------------------------

        [Test]
        public void AnUntouchedWindow_IsTheShippedLayout_BitExact()
        {
            var baseRect = new Rect(312.5f, 16f, 900.25f, 767.7f);
            Rect card = BoatUiWindowLayout.WindowRect(baseRect, default, in Cfg, 1920f, 1080f, 220f);
            Assert.That(card, Is.EqualTo(baseRect),
                        "the default state must return the base rect UNCHANGED — no float round-trip — " +
                        "so shipping the windowing changes nothing until the player reaches for it");
        }

        [Test]
        public void AResizedWindow_ScalesAboutItsCentre_AspectPreserved()
        {
            var baseRect = new Rect(400f, 100f, 300f, 200f);
            var st = new BoatUiWindowState { Scale = 2f };
            Rect card = BoatUiWindowLayout.WindowRect(baseRect, in st, in Cfg, 1920f, 1080f, 0f);
            Assert.That(card.width, Is.EqualTo(600f).Within(1e-3f));
            Assert.That(card.height, Is.EqualTo(400f).Within(1e-3f));
            Assert.That(card.center.x, Is.EqualTo(baseRect.center.x).Within(1e-3f));
            Assert.That(card.width / card.height,
                        Is.EqualTo(baseRect.width / baseRect.height).Within(1e-4f),
                        "one scale, both axes — a rig is never stretched");
        }

        [Test]
        public void TheScale_IsClampedIntoTheSettingsBounds()
        {
            var st = new BoatUiWindowState { Scale = 100f };
            Assert.That(BoatUiWindowLayout.EffectiveScale(in st, in Cfg), Is.EqualTo(Cfg.MaxScale));
            st.Scale = 0.0001f;
            Assert.That(BoatUiWindowLayout.EffectiveScale(in st, in Cfg), Is.EqualTo(Cfg.MinScale));
            st.Scale = 0f;   // unset reads as 1×
            Assert.That(BoatUiWindowLayout.EffectiveScale(in st, in Cfg), Is.EqualTo(1f));
        }

        [Test]
        public void Compact_IsTheGlanceTier_AndBarIsTheStripAlone()
        {
            var baseRect = new Rect(400f, 100f, 300f, 200f);
            var st = new BoatUiWindowState { Collapse = BoatUiCollapse.Compact };
            Rect compact = BoatUiWindowLayout.WindowRect(baseRect, in st, in Cfg, 1920f, 1080f, 0f);
            Assert.That(compact.width, Is.EqualTo(300f * Cfg.CompactScale).Within(1e-3f));
            Assert.That(compact.height, Is.EqualTo(200f * Cfg.CompactScale).Within(1e-3f));

            st.Collapse = BoatUiCollapse.Bar;
            Rect bar = BoatUiWindowLayout.WindowRect(baseRect, in st, in Cfg, 1920f, 1080f, 0f);
            Assert.That(bar.height, Is.EqualTo(0f), "BAR: the card is not drawn at all");
            Assert.That(bar.width, Is.EqualTo(300f).Within(1e-3f),
                        "…but the strip keeps the full footprint's width");
            Rect strip = BoatUiWindowLayout.BarRect(bar, in Cfg);
            Assert.That(strip.height, Is.EqualTo(Cfg.TitleBarPx));
            Assert.That(strip.yMin, Is.EqualTo(bar.yMax));
        }

        [Test]
        public void ADraggedWindow_LandsOnItsCustomCentre_AndStaysOnScreen()
        {
            var baseRect = new Rect(400f, 100f, 300f, 200f);
            var st = new BoatUiWindowState
            {
                HasCustomCenter = true,
                Center01 = new Vector2(0.25f, 0.5f),
            };
            Rect card = BoatUiWindowLayout.WindowRect(baseRect, in st, in Cfg, 1920f, 1080f, 0f);
            Assert.That(card.center.x, Is.EqualTo(0.25f * 1920f).Within(1e-3f));
            Assert.That(card.center.y, Is.EqualTo(0.5f * 1080f).Within(1e-3f));

            // Thrown at a corner: the window is clamped fully on screen, bar headroom included.
            st.Center01 = new Vector2(1f, 1f);
            card = BoatUiWindowLayout.WindowRect(baseRect, in st, in Cfg, 1920f, 1080f, 220f);
            Assert.That(card.xMax, Is.LessThanOrEqualTo(1920f));
            Assert.That(card.yMax + Cfg.TitleBarPx, Is.LessThanOrEqualTo(1080f - 220f + 1e-3f),
                        "the card plus its strip stays below the band's reserve");
            Assert.That(card.xMin, Is.GreaterThanOrEqualTo(0f));
            Assert.That(card.yMin, Is.GreaterThanOrEqualTo(0f));
        }

        [Test]
        public void AWindowTooBigForTheScreen_ShrinksAspectPreserved()
        {
            var baseRect = new Rect(0f, 0f, 800f, 700f);
            var st = new BoatUiWindowState { Scale = 3f };   // 2400×2100 on a 1280×720 screen
            Rect card = BoatUiWindowLayout.WindowRect(baseRect, in st, in Cfg, 1280f, 720f, 120f);
            Assert.That(card.width, Is.LessThanOrEqualTo(1280f));
            Assert.That(card.yMax + Cfg.TitleBarPx, Is.LessThanOrEqualTo(720f - 120f + 1e-2f));
            Assert.That(card.width / card.height, Is.EqualTo(800f / 700f).Within(1e-3f));
        }

        [Test]
        public void TheChromeHitBoxes_AreWhereTheChromeDraws()
        {
            var card = new Rect(100f, 100f, 300f, 200f);
            Rect bar = BoatUiWindowLayout.BarRect(card, in Cfg);
            Rect hide = BoatUiWindowLayout.HideButtonRect(bar, in Cfg);
            Rect tier = BoatUiWindowLayout.CollapseButtonRect(bar, in Cfg);
            Rect grip = BoatUiWindowLayout.GripRect(card, in Cfg);

            Assert.That(BoatUiWindowLayout.HitChrome(hide.center, card, in Cfg),
                        Is.EqualTo(BoatUiChromeHit.Hide));
            Assert.That(BoatUiWindowLayout.HitChrome(tier.center, card, in Cfg),
                        Is.EqualTo(BoatUiChromeHit.Collapse));
            Assert.That(BoatUiWindowLayout.HitChrome(new Vector2(bar.xMin + 4f, bar.center.y),
                                                     card, in Cfg),
                        Is.EqualTo(BoatUiChromeHit.Bar));
            Assert.That(BoatUiWindowLayout.HitChrome(grip.center, card, in Cfg),
                        Is.EqualTo(BoatUiChromeHit.Grip));
            Assert.That(BoatUiWindowLayout.HitChrome(card.center, card, in Cfg),
                        Is.EqualTo(BoatUiChromeHit.None),
                        "the card body is the instrument's, never the chrome's");
            Assert.That(BoatUiWindowLayout.HitChrome(new Vector2(card.xMin - 5f, card.yMin - 5f),
                                                     card, in Cfg),
                        Is.EqualTo(BoatUiChromeHit.None));
        }

        [Test]
        public void TheMoveDrag_TracksThePointer_AndClamps()
        {
            Vector2 c01 = BoatUiWindowLayout.DragCenter01(
                new Vector2(960f, 540f), new Vector2(1000f, 600f), new Vector2(1100f, 700f),
                1920f, 1080f);
            Assert.That(c01.x, Is.EqualTo((960f + 100f) / 1920f).Within(1e-4f));
            Assert.That(c01.y, Is.EqualTo((540f + 100f) / 1080f).Within(1e-4f));

            c01 = BoatUiWindowLayout.DragCenter01(
                new Vector2(960f, 540f), Vector2.zero, new Vector2(-9999f, 99999f), 1920f, 1080f);
            Assert.That(c01.x, Is.EqualTo(0f));
            Assert.That(c01.y, Is.EqualTo(1f));
        }

        [Test]
        public void TheResizeDrag_GrowsWithThePointer_AndClamps()
        {
            // Pull the grip 75 px right on a 300 px card: centred growth, so +150 px of width = 1.5×.
            float s = BoatUiWindowLayout.DragScale(1f, 300f,
                new Vector2(400f, 100f), new Vector2(475f, 100f), in Cfg);
            Assert.That(s, Is.EqualTo(1.5f).Within(1e-3f));

            // Pull DOWN grows too (the grip is the bottom-right corner).
            s = BoatUiWindowLayout.DragScale(1f, 300f,
                new Vector2(400f, 100f), new Vector2(400f, 25f), in Cfg);
            Assert.That(s, Is.EqualTo(1.5f).Within(1e-3f));

            // And it clamps both ways.
            Assert.That(BoatUiWindowLayout.DragScale(1f, 300f,
                new Vector2(400f, 100f), new Vector2(9999f, 100f), in Cfg),
                Is.EqualTo(Cfg.MaxScale));
            Assert.That(BoatUiWindowLayout.DragScale(1f, 300f,
                new Vector2(400f, 100f), new Vector2(-9999f, 100f), in Cfg),
                Is.EqualTo(Cfg.MinScale));
        }

        // ---- the settings -------------------------------------------------------------------------

        [Test]
        public void TheShippedAsset_CarriesTheWindowBlock_AtTheCodeDefaults()
        {
            // A YAML block a struct field is missing from deserializes that field to ZERO, not to
            // .Default — the #420 trap. This pins the shipped asset's keys against the code defaults
            // (exact, because the block is brand new and the owner has not tuned it yet; loosen to
            // bounds if he does — the RodFight guard-rail pattern).
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigAssetPath);
            Assert.That(config, Is.Not.Null, "GameConfig.asset not found at " + ConfigAssetPath);

            BoatUiWindowSettings a = config.BoatUiWindows;
            BoatUiWindowSettings d = BoatUiWindowSettings.Default;
            Assert.That(a.TitleBarPx, Is.EqualTo(d.TitleBarPx), "TitleBarPx missing from the asset?");
            Assert.That(a.ChromeButtonPx, Is.EqualTo(d.ChromeButtonPx));
            Assert.That(a.GripPx, Is.EqualTo(d.GripPx));
            Assert.That(a.CompactScale, Is.EqualTo(d.CompactScale));
            Assert.That(a.MinScale, Is.EqualTo(d.MinScale));
            Assert.That(a.MaxScale, Is.EqualTo(d.MaxScale));
        }
    }
}
