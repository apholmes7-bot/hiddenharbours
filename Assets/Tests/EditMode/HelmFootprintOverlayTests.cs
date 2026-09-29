using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using HiddenHarbours.UI;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// THE HELM DASH BAND, H1 — <b>nothing overlaps what the helm covers</b> (ADR 0050). The surfaces
    /// that draw near the screen's bottom edge — the dev toast (the one #416 overlay that still draws;
    /// the other three now speak through the interact popup and the quest note), the interact popup, the
    /// quest note and the nav cluster — each read the Core seam and keep out of it:
    ///
    /// <list type="bullet">
    /// <item><b>a full-width band</b> is the new bottom edge — each rises onto it, its own distance from
    /// the edge kept, so what was stacked is stacked the same way;</item>
    /// <item><b>a card</b> is stepped over by the surface's own gap where the surface reaches it — and
    /// the nav cluster steps BESIDE it first (the S4.5 move, now the tiller's card's too: the audit
    /// l.453).</item>
    /// </list>
    ///
    /// <para>Every one is checked at the six screens the band is sized for, against a band of 144·k
    /// (k = max(1, H div 1080)) and against the tiller's 120×244 card at ×1 with its title strip — the
    /// card the host publishes for it. With nothing covered, each is where it is on 91ff97f2, bit-exact.
    /// The last test is rule 4: every reader reaches the seam through Core, never UI.</para>
    /// </summary>
    public class HelmFootprintOverlayTests
    {
        private static readonly Vector2Int[] Screens =
        {
            new Vector2Int(1280, 720), new Vector2Int(1280, 800), new Vector2Int(1920, 1080),
            new Vector2Int(2560, 1440), new Vector2Int(3440, 1440), new Vector2Int(3840, 2160),
        };

        // The toast as every scene serializes it (Greywick, Nine Mile Creek and St Peters all hold
        // these three, the component's defaults).
        private const float ToastBaseY = 230f, ToastSpacing = 38f;
        private const int ToastSlots = 4;

        /// <summary>A hundredth of a pixel: float rounding in a lift that lands a box exactly on an
        /// edge, not layout.</summary>
        private const float Eps = 0.01f;

        [SetUp]
        public void SetUp() => HelmFootprint.Reset();

        [TearDown]
        public void TearDown() => HelmFootprint.Reset();

        private static GameConfig Shipped()
        {
            var cfg = UnityEditor.AssetDatabase.LoadAssetAtPath<GameConfig>(
                "Assets/_Project/Data/Config/GameConfig.asset");
            Assert.IsNotNull(cfg, "the shipped GameConfig asset");
            return cfg;
        }

        private static string Name(Vector2Int s) => $"{s.x}x{s.y}";

        private static HelmFootprintArea Band(Vector2Int s)
            => HelmFootprintArea.FullWidthBand(144f * Mathf.Max(1, s.y / 1080));

        /// <summary>The tiller's card at ×1 where the host puts it, with its title strip — exactly what
        /// <see cref="HelmOverlayHost"/> publishes for it.</summary>
        private static HelmFootprintArea TillerCard(GameConfig cfg, Vector2Int s)
            => HelmOverlayHost.FootprintOf(
                HelmOverlayLayout.CardRect(false, TillerRigRender.W, TillerRigRender.H, cfg.HelmOverlay, s.x, s.y),
                cfg.BoatUiWindows.TitleBarPx);

        private static bool Meets(in HelmFootprintArea covered, Rect box)
            => covered.Overlaps(Rect.MinMaxRect(box.xMin + Eps, box.yMin + Eps, box.xMax - Eps, box.yMax - Eps));

        private static Rect Raised(Rect r, float dy) => new Rect(r.x, r.y + dy, r.width, r.height);

        // ---- each surface's box, in screen px, placed for what is covered ----------------------------

        private static Rect ToastHome(Vector2Int s)
            => DevToast.StackScreenRect(s.x, s.y, ToastBaseY, ToastSpacing, ToastSlots);

        private static Rect ToastPlaced(in HelmFootprintArea covered, Vector2Int s)
        {
            float lift = DevToast.LiftRef(in covered, s.x, s.y, ToastBaseY, ToastSpacing, ToastSlots);
            // SlotY raises every slot by the lift, so the placed stack is the stack on a raised base.
            return DevToast.StackScreenRect(s.x, s.y, ToastBaseY + lift, ToastSpacing, ToastSlots);
        }

        private static Rect PopupPlaced(in HelmFootprintArea covered, Vector2Int s)
        {
            Vector2 home = InteractPopupLayout.PanelAnchoredPosition();
            Vector2 placed = InteractPopupLayout.PanelAnchoredPosition(in covered, s.x, s.y);
            Assert.AreEqual(home.x, placed.x, $"{Name(s)}: the popup keeps its column");
            return Raised(InteractPopupLayout.PanelScreenRect(s.x, s.y),
                          (placed.y - home.y) * HudBandLayout.ScaleFactor(s.x, s.y));
        }

        private static Rect NotePlaced(in QuestPanelFit fit, in HelmFootprintArea covered, Vector2Int s)
        {
            Vector2 home = QuestPanelLayout.AnchoredPosition(in fit);
            Vector2 placed = QuestPanelLayout.AnchoredPosition(in fit, in covered, s.x, s.y);
            Assert.AreEqual(home.x, placed.x, $"{Name(s)}: the note keeps its column");
            return Raised(QuestPanelLayout.NoteScreenRect(in fit, s.x, s.y), placed.y - home.y);
        }

        private static Rect NavPlaced(NavClusterPlacement p, in HelmFootprintArea covered, Vector2Int s)
            => Raised(HelmHudSuppression.NavClusterScreenRect(p, s.x, s.y),
                      HelmHudSuppression.NavClusterLiftRef(p, in covered, s.x, s.y)
                      * HudBandLayout.ScaleFactor(s.x, s.y));

        private static HelmFit CompassLessDash
            => new HelmFit(ConsoleRigKind.Cape, SounderKind.None, CompassMount.None, false, false);

        // ===== the dev toast =========================================================================

        [Test]
        public void TheToast_StandsAboveAFullWidthBand_AtEveryScreen()
        {
            var failures = new List<string>();
            foreach (Vector2Int s in Screens)
            {
                HelmFootprintArea band = Band(s);
                Rect home = ToastHome(s), placed = ToastPlaced(in band, s);
                if (Meets(in band, placed)) failures.Add($"{Name(s)}: on the band ({placed})");
                if (Mathf.Abs(placed.yMin - (home.yMin + band.TopPx)) > Eps)
                    failures.Add($"{Name(s)}: foot {placed.yMin}, not the band's top plus its own " +
                                 $"{home.yMin} ({home.yMin + band.TopPx})");
            }
            CollectionAssert.IsEmpty(failures, string.Join("\n", failures));
        }

        [Test]
        public void TheToast_StepsOverTheTillersCard_OnlyWhereItReachesIt()
        {
            GameConfig cfg = Shipped();
            var failures = new List<string>();
            var moved = new List<string>();
            foreach (Vector2Int s in Screens)
            {
                HelmFootprintArea card = TillerCard(cfg, s);
                Rect home = ToastHome(s), placed = ToastPlaced(in card, s);
                if (Meets(in card, placed)) failures.Add($"{Name(s)}: on the card ({placed} vs {card})");
                if (placed != home)
                {
                    moved.Add(Name(s));
                    if (Mathf.Abs(placed.yMin - card.TopPx) > Eps)
                        failures.Add($"{Name(s)}: lifted to {placed.yMin}, not onto the card's top {card.TopPx}");
                }
            }
            CollectionAssert.IsEmpty(failures, string.Join("\n", failures));
            CollectionAssert.AreEqual(new[] { "1280x720", "1280x800" }, moved,
                "the stack reaches the tiller's card only where the canvas scale keeps it low; everywhere " +
                "else it already clears it and stays put");
        }

        // ===== the interact popup ====================================================================

        [Test]
        public void ThePopup_StandsAboveAFullWidthBand_AtEveryScreen()
        {
            var failures = new List<string>();
            foreach (Vector2Int s in Screens)
            {
                HelmFootprintArea band = Band(s);
                Rect home = InteractPopupLayout.PanelScreenRect(s.x, s.y), placed = PopupPlaced(in band, s);
                if (Meets(in band, placed)) failures.Add($"{Name(s)}: on the band ({placed})");
                // Its margin above the edge is kept; at 3440×1440 its home hangs off the screen (the
                // reference host is pinned top-left — main's, reported), so there it lands ON the band.
                float want = Mathf.Max(home.yMin, 0f) + band.TopPx;
                if (Mathf.Abs(placed.yMin - want) > Eps)
                    failures.Add($"{Name(s)}: foot {placed.yMin}, want {want}");
            }
            CollectionAssert.IsEmpty(failures, string.Join("\n", failures));
        }

        [Test]
        public void ThePopup_NeverMeetsTheTillersCard_SoItNeverMoves()
        {
            GameConfig cfg = Shipped();
            foreach (Vector2Int s in Screens)
            {
                HelmFootprintArea card = TillerCard(cfg, s);
                Assert.IsFalse(Meets(in card, InteractPopupLayout.PanelScreenRect(s.x, s.y)),
                               $"{Name(s)}: the popup's corner is clear of the centred card");
                Vector2 placed = InteractPopupLayout.PanelAnchoredPosition(in card, s.x, s.y);
                Assert.AreEqual(InteractPopupLayout.PanelAnchoredPosition(), placed, $"{Name(s)}: unmoved");
            }
        }

        // ===== the quest note ========================================================================

        [Test]
        public void TheQuestNote_StandsAboveAFullWidthBand_AtEveryScreenAndLength()
        {
            var failures = new List<string>();
            foreach (Vector2Int s in Screens)
            {
                HelmFootprintArea band = Band(s);
                for (int rows = 1; rows <= QuestPanelLayout.MaxLines; rows++)
                {
                    QuestPanelFit fit = QuestPanelLayout.Fit(rows, s.x, s.y);
                    Rect home = QuestPanelLayout.NoteScreenRect(in fit, s.x, s.y);
                    Rect placed = NotePlaced(in fit, in band, s);
                    if (Meets(in band, placed)) failures.Add($"{Name(s)} ×{rows}: on the band ({placed})");
                    if (Mathf.Abs(placed.yMin - (home.yMin + band.TopPx)) > Eps)
                        failures.Add($"{Name(s)} ×{rows}: foot {placed.yMin}, want {home.yMin + band.TopPx}");
                }
            }
            CollectionAssert.IsEmpty(failures, string.Join("\n", failures));
        }

        [Test]
        public void TheQuestNote_NeverMeetsTheTillersCard_SoItNeverMoves()
        {
            GameConfig cfg = Shipped();
            foreach (Vector2Int s in Screens)
            {
                HelmFootprintArea card = TillerCard(cfg, s);
                for (int rows = 1; rows <= QuestPanelLayout.MaxLines; rows++)
                {
                    QuestPanelFit fit = QuestPanelLayout.Fit(rows, s.x, s.y);
                    Assert.IsFalse(Meets(in card, QuestPanelLayout.NoteScreenRect(in fit, s.x, s.y)),
                                   $"{Name(s)} ×{rows}: the note's corner is clear of the centred card");
                    Assert.AreEqual(QuestPanelLayout.AnchoredPosition(in fit),
                                    QuestPanelLayout.AnchoredPosition(in fit, in card, s.x, s.y),
                                    $"{Name(s)} ×{rows}: unmoved");
                }
            }
        }

        // ===== the nav cluster =======================================================================

        [Test]
        public void TheNavCluster_StandsAboveAFullWidthBand_WhereverItIsPlaced()
        {
            var failures = new List<string>();
            foreach (Vector2Int s in Screens)
            {
                HelmFootprintArea band = Band(s);
                // Home (a boat with no dash) and moved (a dash with no compass of its own): a band never
                // changes WHERE the cluster goes, only how high.
                var cases = new[]
                {
                    (HelmControlStyle.Tiller, HelmFit.None, NavClusterPlacement.BottomCentre),
                    (HelmControlStyle.Lever, CompassLessDash, NavClusterPlacement.ClearOfTheDash),
                };
                foreach ((HelmControlStyle style, HelmFit fit, NavClusterPlacement want) in cases)
                {
                    NavClusterPlacement p = HelmHudSuppression.NavCluster(true, style, in fit, in band, s.x, s.y);
                    if (p != want) { failures.Add($"{Name(s)} {style}: placed {p}, want {want}"); continue; }
                    Rect placed = NavPlaced(p, in band, s);
                    if (Meets(in band, placed)) failures.Add($"{Name(s)} {p}: on the band ({placed})");
                    if (Mathf.Abs(placed.yMin - band.TopPx) > Eps)
                        failures.Add($"{Name(s)} {p}: its lowest box at {placed.yMin}, not on the band's " +
                                     $"top {band.TopPx}");
                }
            }
            CollectionAssert.IsEmpty(failures, string.Join("\n", failures));
        }

        [Test]
        public void TheNavCluster_StepsBesideTheTillersCard_AtEveryScreen()
        {
            GameConfig cfg = Shipped();
            foreach (Vector2Int s in Screens)
            {
                HelmFootprintArea card = TillerCard(cfg, s);
                Assert.AreEqual(NavClusterPlacement.BottomCentre,
                                HelmHudSuppression.NavCluster(true, HelmControlStyle.Tiller, HelmFit.None),
                                "S4.5 alone leaves it bottom-centre at a tiller…");
                Assert.IsTrue(Meets(in card, HelmHudSuppression.NavClusterScreenRect(
                                  NavClusterPlacement.BottomCentre, s.x, s.y)),
                              $"{Name(s)}: …where it sits on the tiller's card (the audit l.453)");

                NavClusterPlacement p = HelmHudSuppression.NavCluster(true, HelmControlStyle.Tiller, HelmFit.None,
                                                                      in card, s.x, s.y);
                Assert.AreEqual(NavClusterPlacement.ClearOfTheDash, p, $"{Name(s)}: so it steps beside the card");
                Assert.AreEqual(0f, HelmHudSuppression.NavClusterLiftRef(p, in card, s.x, s.y),
                                $"{Name(s)}: and beside is enough — it does not rise");
                Assert.IsFalse(Meets(in card, NavPlaced(p, in card, s)), $"{Name(s)}: clear of the card");
            }
        }

        [Test]
        public void UnderABand_TheOrderIsKept_TheToastStillAboveTheNavCluster()
        {
            foreach (Vector2Int s in Screens)
            {
                HelmFootprintArea band = Band(s);
                Rect nav = NavPlaced(NavClusterPlacement.BottomCentre, in band, s);
                Rect toast = ToastPlaced(in band, s);
                Assert.GreaterOrEqual(toast.yMin, nav.yMax - Eps,
                    $"{Name(s)}: the floor rose under both, so the toast still stands above the cluster");
            }
        }

        // ===== nothing covered: main's places, bit-exact =============================================

        [Test]
        public void WithNothingCovered_EverySurfaceIsWhereItIsOnMain_BitExact()
        {
            HelmFootprintArea none = HelmFootprintArea.None;
            HelmFit[] fits =
            {
                HelmFit.None, CompassLessDash,
                new HelmFit(ConsoleRigKind.Console, SounderKind.Depth, CompassMount.Dome, false, false),
                new HelmFit(ConsoleRigKind.Novi, SounderKind.Fish, CompassMount.Flush, true, true),
            };
            foreach (Vector2Int s in Screens)
            {
                // The toast: main's expression, _baseY + slot · _lineSpacing.
                Assert.AreEqual(0f, DevToast.LiftRef(in none, s.x, s.y, ToastBaseY, ToastSpacing, ToastSlots));
                for (int slot = 0; slot < ToastSlots; slot++)
                    Assert.AreEqual(ToastBaseY + slot * ToastSpacing,
                                    DevToast.SlotY(ToastBaseY, 0f, slot, ToastSpacing), $"{Name(s)} slot {slot}");

                // The popup: main's lower-right, 16 in from both edges of the 1280×720 host.
                Vector2 popup = InteractPopupLayout.PanelAnchoredPosition(in none, s.x, s.y);
                Assert.AreEqual(964f, popup.x, Name(s));
                Assert.AreEqual(-660f, popup.y, Name(s));

                // The note: main's corner, 8 of its own pixels in.
                for (int rows = 1; rows <= QuestPanelLayout.MaxLines; rows++)
                {
                    QuestPanelFit fit = QuestPanelLayout.Fit(rows, s.x, s.y);
                    Vector2 note = QuestPanelLayout.AnchoredPosition(in fit, in none, s.x, s.y);
                    Assert.AreEqual(-8f * fit.Scale, note.x, $"{Name(s)} ×{rows}");
                    Assert.AreEqual(8f * fit.Scale, note.y, $"{Name(s)} ×{rows}");
                }

                // The nav cluster: the S4.5 answer, unlifted, for every helm.
                foreach (bool aboard in new[] { false, true })
                foreach (HelmControlStyle style in new[] { HelmControlStyle.None, HelmControlStyle.Tiller,
                                                           HelmControlStyle.Lever })
                foreach (HelmFit fit in fits)
                {
                    NavClusterPlacement p = HelmHudSuppression.NavCluster(aboard, style, in fit, in none, s.x, s.y);
                    Assert.AreEqual(HelmHudSuppression.NavCluster(aboard, style, in fit), p,
                                    $"{Name(s)} aboard={aboard} {style} {fit.Rig}/{fit.Compass}");
                    Assert.AreEqual(0f, HelmHudSuppression.NavClusterLiftRef(p, in none, s.x, s.y));
                }
            }
        }

        [Test]
        public void TheNavClustersGeometry_IsMainsNumbers_GivenOneHome()
        {
            // HudController built the five lines from literals on main; they now live beside the rule
            // that reads them, and must be the same numbers.
            Assert.AreEqual(0.2f, HelmHudSuppression.HomeMinX01);
            Assert.AreEqual(0.8f, HelmHudSuppression.HomeMaxX01);
            Assert.AreEqual(40f, HelmHudSuppression.ApparentWindTopRef);
            Assert.AreEqual(70f, HelmHudSuppression.SetDriftTopRef);
            Assert.AreEqual(118f, HelmHudSuppression.RibbonTopRef);
            Assert.AreEqual(146f, HelmHudSuppression.NeedleTopRef);
            Assert.AreEqual(188f, HelmHudSuppression.HeadingTopRef);
            Assert.AreEqual(0.34f, HelmHudSuppression.ClearWidth01);
            Assert.AreEqual(16f, HelmHudSuppression.ClearMarginXRef);
            // …and the label box main gave each line, 56 tall and pivoted on its top, which is why the
            // lowest box hangs 16 below the screen's edge on the 1280×720 canvas.
            Assert.AreEqual(Rect.MinMaxRect(256f, -16f, 1024f, 188f),
                            HelmHudSuppression.NavClusterScreenRect(NavClusterPlacement.BottomCentre, 1280, 720));
            Assert.AreEqual(Rect.MinMaxRect(16f, -16f, 0.34f * 1280f + 16f, 188f),
                            HelmHudSuppression.NavClusterScreenRect(NavClusterPlacement.ClearOfTheDash, 1280, 720));
            Assert.AreEqual(default(Rect),
                            HelmHudSuppression.NavClusterScreenRect(NavClusterPlacement.Hidden, 1280, 720));
        }

        // ===== the three #416 overlays that no longer draw ===========================================

        [Test]
        public void TheThreeRetiredOverlays_DrawNothingOfTheirOwn()
        {
            // The control-switch hint and the interactor's prompt speak through the interact popup
            // (2026-08-19), the onboarding banner through the quest note (2026-08-22): whatever keeps
            // those two clear keeps these three clear. Pinned so a revival has to come through here.
            string[] retired =
            {
                "Assets/_Project/Code/Player/ControlSwitcher.cs",
                "Assets/_Project/Code/World/WorldInteractor.cs",
                "Assets/_Project/Code/World/OnboardingDirector.cs",
            };
            string[] drawing = { "Canvas", "UnityEngine.UI", "OnGUI", "TextMesh", "new GameObject(" };
            var failures = new List<string>();
            foreach (string path in retired)
            {
                Assert.IsTrue(File.Exists(path), path);
                string src = File.ReadAllText(path);
                failures.AddRange(drawing.Where(src.Contains).Select(d => $"{Path.GetFileName(path)}: {d}"));
            }
            CollectionAssert.IsEmpty(failures, string.Join("\n", failures));
        }

        // ===== rule 4 ================================================================================

        [Test]
        public void EveryReader_ReachesTheSeamThroughCore_NeverUi()
        {
            var failures = new List<string>();
            foreach (string asmdef in new[]
                     {
                         "Assets/_Project/Code/Player/HiddenHarbours.Player.asmdef",
                         "Assets/_Project/Code/World/HiddenHarbours.World.asmdef",
                         "Assets/_Project/Code/App/HiddenHarbours.App.asmdef",
                     })
            {
                if (File.ReadAllText(asmdef).Contains("\"HiddenHarbours.UI\""))
                    failures.Add($"{Path.GetFileName(asmdef)} references HiddenHarbours.UI");
            }

            IEnumerable<string> runtime = Directory.GetFiles("Assets/_Project/Code/Player", "*.cs", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles("Assets/_Project/Code/World", "*.cs", SearchOption.AllDirectories))
                .Concat(Directory.GetFiles("Assets/_Project/Code/App", "*.cs", SearchOption.AllDirectories)
                                 .Where(p => !p.Replace('\\', '/').Contains("/Editor/")));
            failures.AddRange(runtime.Where(p => File.ReadAllText(p).Contains("HiddenHarbours.UI"))
                                     .Select(p => $"{p} names HiddenHarbours.UI"));

            foreach (string reader in new[]
                     {
                         "Assets/_Project/Code/Player/DevToast.cs",
                         "Assets/_Project/Code/World/QuestPanelPresenter.cs",
                         "Assets/_Project/Code/App/CameraFollow.cs",
                     })
            {
                if (!File.ReadAllText(reader).Contains("HelmFootprint.Current"))
                    failures.Add($"{Path.GetFileName(reader)} does not read the Core seam");
            }
            CollectionAssert.IsEmpty(failures, string.Join("\n", failures));
        }
    }
}
