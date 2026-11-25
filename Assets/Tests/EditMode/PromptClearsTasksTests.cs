using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.UI;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The action prompt never sits on the TASKS note</b> — the owner's 2026-11-13 "player
    /// tips/actions also cover the ui for objectives".
    ///
    /// <para>Both live in the lower right: the prompt is UI's (<see cref="InteractPopupLayout"/>), the
    /// note is World's (<see cref="QuestPanelLayout"/>), and neither may know the other (rule 4). The
    /// note states its box through the Core seam <see cref="QuestNoteFootprint"/>; the prompt reads it
    /// and stands above the note (or beside it, when that is the shorter move).</para>
    ///
    /// <para>All of it is pure maths, proved here with no canvas: at the six screens the HUD is sized
    /// for, for every height the note can be (1 to <see cref="QuestPanelLayout.MaxLines"/> lines), with
    /// nothing at the helm, with the helm's full-width band, and with the tiller's card.</para>
    /// </summary>
    public class PromptClearsTasksTests
    {
        // The same six HelmFootprintOverlayTests proves the band at.
        private static readonly Vector2Int[] Screens =
        {
            new Vector2Int(1280, 720), new Vector2Int(1280, 800), new Vector2Int(1920, 1080),
            new Vector2Int(2560, 1440), new Vector2Int(3440, 1440), new Vector2Int(3840, 2160),
        };

        /// <summary>A hundredth of a pixel: float rounding where a box lands exactly on an edge.</summary>
        private const float Eps = 0.01f;

        [SetUp]
        public void SetUp()
        {
            HelmFootprint.Reset();
            QuestNoteFootprint.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            HelmFootprint.Reset();
            QuestNoteFootprint.Reset();
        }

        private static string Name(Vector2Int s) => $"{s.x}x{s.y}";

        private static GameConfig Shipped()
        {
            var cfg = UnityEditor.AssetDatabase.LoadAssetAtPath<GameConfig>(
                "Assets/_Project/Data/Config/GameConfig.asset");
            Assert.IsNotNull(cfg, "the shipped GameConfig asset");
            return cfg;
        }

        private static HelmFootprintArea Band(Vector2Int s)
            => HelmOverlayHost.FootprintOf(HelmOverlayHost.HelmCardKind.Band, default, s.y);

        private static HelmFootprintArea TillerCard(GameConfig cfg, Vector2Int s)
            => HelmOverlayHost.FootprintOf(
                HelmOverlayHost.HelmCardKind.Tiller,
                HelmOverlayLayout.CardRect(TillerRigRender.W, TillerRigRender.H, cfg.HelmOverlay, s.x, s.y),
                s.y);

        private static Rect Shrunk(Rect r)
            => Rect.MinMaxRect(r.xMin + Eps, r.yMin + Eps, r.xMax - Eps, r.yMax - Eps);

        /// <summary>The note exactly as the presenter publishes it: fitted to the screen, lifted for
        /// the helm.</summary>
        private static Rect Note(int lines, in HelmFootprintArea covered, Vector2Int s)
        {
            QuestPanelFit fit = QuestPanelLayout.Fit(lines, s.x, s.y);
            return QuestPanelLayout.NoteScreenRect(in fit, in covered, s.x, s.y);
        }

        private static void AssertClear(string what, in HelmFootprintArea covered, int lines, Vector2Int s)
        {
            Rect note = Note(lines, in covered, s);
            Rect home = InteractPopupLayout.PanelScreenRect(s.x, s.y);
            Rect panel = InteractPopupLayout.PanelScreenRect(in covered, note, s.x, s.y);
            string at = $"{Name(s)}, {what}, {lines} line(s): prompt {panel}, note {note}";

            Assert.IsFalse(Shrunk(panel).Overlaps(note), $"{at} — the prompt is on the note");
            Assert.IsFalse(covered.Overlaps(Shrunk(panel)), $"{at} — the prompt is on the helm's UI");
            Assert.AreEqual(home.width, panel.width, Eps, $"{at} — the prompt keeps its size");
            Assert.AreEqual(home.height, panel.height, Eps, $"{at} — the prompt keeps its size");
            Assert.GreaterOrEqual(panel.yMin, home.yMin - Eps, $"{at} — the prompt never moves down");
            Assert.LessOrEqual(panel.xMin, home.xMin + Eps, $"{at} — the prompt never moves right");
            // The move never sends a prompt that started on screen off the top of it.
            if (home.yMax <= s.y) Assert.LessOrEqual(panel.yMax, s.y + Eps, $"{at} — the prompt left the screen");

            // What the canvas is told agrees with the rect proved above.
            float scale = HudBandLayout.ScaleFactor(s.x, s.y);
            Vector2 homeAt = InteractPopupLayout.PanelAnchoredPosition();
            Vector2 placedAt = InteractPopupLayout.PanelAnchoredPosition(in covered, note, s.x, s.y);
            Assert.AreEqual(panel.x - home.x, (placedAt.x - homeAt.x) * scale, Eps, $"{at} — anchored x");
            Assert.AreEqual(panel.y - home.y, (placedAt.y - homeAt.y) * scale, Eps, $"{at} — anchored y");
        }

        [Test]
        public void ThePromptNeverOverlapsTheNote_AtEveryScreen_EveryNoteHeight_EveryHelmState()
        {
            GameConfig cfg = Shipped();
            foreach (Vector2Int s in Screens)
            {
                for (int lines = 1; lines <= QuestPanelLayout.MaxLines; lines++)
                {
                    AssertClear("nothing at the helm", HelmFootprintArea.None, lines, s);
                    AssertClear("the helm band", Band(s), lines, s);
                    AssertClear("the tiller card", TillerCard(cfg, s), lines, s);
                }
            }
        }

        [Test]
        public void WhereThePromptReachedTheNote_ItStandsItsOwnMarginAboveIt_ColumnKept()
        {
            // The 16:9 screens, where the prompt's home really is the note's corner.
            foreach (Vector2Int s in Screens)
            {
                if (s.x * 9 != s.y * 16) continue;
                float gap = InteractPopupLayout.MarginY * HudBandLayout.ScaleFactor(s.x, s.y);
                for (int lines = 1; lines <= QuestPanelLayout.MaxLines; lines++)
                {
                    Rect note = Note(lines, HelmFootprintArea.None, s);
                    Rect home = InteractPopupLayout.PanelScreenRect(s.x, s.y);
                    Assert.IsTrue(home.Overlaps(note),
                                  $"{Name(s)}, {lines} line(s): at home the prompt IS on the note (the bug)");

                    Rect panel = InteractPopupLayout.PanelScreenRect(HelmFootprintArea.None, note, s.x, s.y);
                    Assert.AreEqual(home.x, panel.x, Eps, $"{Name(s)}, {lines} line(s): column kept");
                    Assert.AreEqual(note.yMax + gap, panel.yMin, Eps,
                                    $"{Name(s)}, {lines} line(s): one margin above the note");
                }
            }
        }

        [Test]
        public void WithNoNote_ThePromptIsExactlyWhereTheHelmRulePutsIt()
        {
            GameConfig cfg = Shipped();
            foreach (Vector2Int s in Screens)
            {
                HelmFootprintArea[] helms = { HelmFootprintArea.None, Band(s), TillerCard(cfg, s) };
                foreach (HelmFootprintArea covered in helms)
                {
                    Vector2 before = InteractPopupLayout.PanelAnchoredPosition(in covered, s.x, s.y);
                    Vector2 after = InteractPopupLayout.PanelAnchoredPosition(in covered, default, s.x, s.y);
                    Assert.AreEqual(before, after, $"{Name(s)}, {covered}: no note, no change (bit-exact)");
                }

                Assert.AreEqual(InteractPopupLayout.PanelAnchoredPosition(),
                                InteractPopupLayout.PanelAnchoredPosition(HelmFootprintArea.None, default, s.x, s.y),
                                $"{Name(s)}: nothing covered, no note: home");
                Assert.AreEqual(InteractPopupLayout.PanelScreenRect(s.x, s.y),
                                InteractPopupLayout.PanelScreenRect(HelmFootprintArea.None, default, s.x, s.y),
                                $"{Name(s)}: nothing covered, no note: the home rect");
            }
        }

        [Test]
        public void ANoteThatDoesNotReachThePrompt_MovesNothing()
        {
            var s = new Vector2Int(1920, 1080);
            Rect home = InteractPopupLayout.PanelScreenRect(s.x, s.y);
            var farAway = new Rect(0f, s.y - 100f, 200f, 100f);   // top-left corner of the screen
            Assert.AreEqual(Vector2.zero,
                            InteractPopupLayout.PlacementOffsetPx(HelmFootprintArea.None, farAway, s.x, s.y));
            Assert.AreEqual(home, InteractPopupLayout.PanelScreenRect(HelmFootprintArea.None, farAway, s.x, s.y));
        }

        [Test]
        public void TheHelmBandStillLiftsThePrompt_AndTheNoteLiftsItFurther()
        {
            foreach (Vector2Int s in Screens)
            {
                HelmFootprintArea band = Band(s);
                Rect note = Note(QuestPanelLayout.MaxLines, in band, s);
                float helmOnly = InteractPopupLayout.PlacementOffsetPx(in band, default, s.x, s.y).y;
                Vector2 both = InteractPopupLayout.PlacementOffsetPx(in band, note, s.x, s.y);

                Assert.Greater(helmOnly, 0f, $"{Name(s)}: the band alone lifts the prompt");
                Assert.GreaterOrEqual(both.y, helmOnly - Eps, $"{Name(s)}: the note never undoes the helm's lift");
                Assert.GreaterOrEqual(note.yMin, band.TopPx - Eps, $"{Name(s)}: the note stands on the band");
            }
        }

        [Test]
        public void ATallThinNote_IsSteppedBeside_BecauseThatIsTheShorterMove()
        {
            // Not a shape the shipped note takes; it pins the second half of the rule.
            var s = new Vector2Int(1920, 1080);
            float scale = HudBandLayout.ScaleFactor(s.x, s.y);
            Rect home = InteractPopupLayout.PanelScreenRect(s.x, s.y);
            var tall = new Rect(s.x - 120f, home.yMin, 96f, 900f);

            Vector2 move = InteractPopupLayout.PlacementOffsetPx(HelmFootprintArea.None, tall, s.x, s.y);
            Rect panel = InteractPopupLayout.PanelScreenRect(HelmFootprintArea.None, tall, s.x, s.y);

            Assert.AreEqual(0f, move.y, Eps, "row kept");
            Assert.Less(move.x, 0f, "moved left");
            Assert.AreEqual(tall.xMin - InteractPopupLayout.MarginX * scale, panel.xMax, Eps,
                            "one margin left of the note");
            Assert.IsFalse(Shrunk(panel).Overlaps(tall), "clear of the note");
        }

        // ---- the seam -------------------------------------------------------------------------------

        [Test]
        public void TheSeam_RaisesOnAChange_AndDropsARestatement()
        {
            int raised = 0;
            QuestNoteFootprintChanged last = default;
            System.Action<QuestNoteFootprintChanged> on = e => { raised++; last = e; };
            EventBus.Subscribe(on);
            try
            {
                Assert.IsTrue(QuestNoteFootprint.IsNone, "no note to begin with");

                var box = new Rect(10f, 20f, 300f, 100f);
                QuestNoteFootprint.Publish(box);
                Assert.AreEqual(1, raised, "a new box is said once");
                Assert.AreEqual(box, last.Current);
                Assert.AreEqual(box, QuestNoteFootprint.Current);
                Assert.IsFalse(QuestNoteFootprint.IsNone);

                QuestNoteFootprint.Publish(box);
                Assert.AreEqual(1, raised, "the same box again is dropped");

                QuestNoteFootprint.Clear();
                Assert.AreEqual(2, raised, "taking the note down is a change");
                Assert.AreEqual(box, last.Previous);
                Assert.IsTrue(QuestNoteFootprint.IsNone);

                QuestNoteFootprint.Publish(new Rect(5f, 5f, 0f, 40f));
                Assert.AreEqual(2, raised, "every empty box is the same 'no note'");
            }
            finally
            {
                EventBus.Unsubscribe(on);
            }
        }
    }
}
