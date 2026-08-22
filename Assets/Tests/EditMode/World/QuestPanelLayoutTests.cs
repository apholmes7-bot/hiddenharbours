using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// <b>The corner quest note's geometry</b> — the lower-right leaf that replaced the bottom-centre
    /// banner on 2026-08-22.
    ///
    /// <para>Two things are worth pinning and one is worth guarding. Pinned: the note is a whole-scale
    /// notebook leaf, and it is pinned to the LOWER-RIGHT corner — bottom-centre belongs to the nav
    /// cluster and the helm dash, and a note that drifted back there would be sitting on the compass.
    /// Guarded: every shipped nudge has to FIT, because a line that overflows loses its tail silently
    /// in play and only the writing lane can fix it.</para>
    /// </summary>
    public sealed class QuestPanelLayoutTests
    {
        // ---- wrapping -----------------------------------------------------------------------------

        [Test]
        public void NoText_IsNoNote()
        {
            Assert.IsEmpty(QuestPanelLayout.Rows(null), "null is 'nothing to say', not an empty page");
            Assert.IsEmpty(QuestPanelLayout.Rows(""));
        }

        [Test]
        public void AShortLine_TakesOneRow()
        {
            List<string> rows = QuestPanelLayout.Rows("Dig clams.");

            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual("Dig clams.", rows[0]);
        }

        [Test]
        public void EveryRow_FitsTheColumnCount()
        {
            foreach (string line in EveryShippedNudge())
            {
                foreach (string row in QuestPanelLayout.Rows(line))
                {
                    Assert.LessOrEqual(row.Length, QuestPanelLayout.Cols,
                        $"a wrapped row overhangs the note's margin: \"{row}\"");
                }
            }
        }

        /// <summary>
        /// <b>⚠ The writing lane's guard.</b> A nudge longer than the note loses its tail, and in play
        /// that is indistinguishable from a sentence that was written badly. This fires the moment
        /// someone authors a step too long for the corner — at which point the fix is the copy, or a
        /// deliberate decision to raise <see cref="QuestPanelLayout.MaxLines"/>.
        /// </summary>
        [Test]
        public void EveryShippedNudge_FitsTheNote_WithoutLosingItsTail()
        {
            foreach (string line in EveryShippedNudge())
            {
                List<string> rows = QuestPanelLayout.Rows(line);
                QuestPanelFit fit = QuestPanelLayout.Fit(rows.Count, 1280, 720);

                Assert.IsFalse(fit.Overflowed,
                    $"this nudge needs more than {QuestPanelLayout.MaxLines} lines at " +
                    $"{QuestPanelLayout.Cols} columns: \"{line}\"");
            }
        }

        // ---- the fit ------------------------------------------------------------------------------

        [Test]
        public void TheScale_IsAlwaysAWholeNumber_AndNeverZero()
        {
            foreach (Vector2Int screen in Screens())
            {
                QuestPanelFit fit = QuestPanelLayout.Fit(3, screen.x, screen.y);

                Assert.GreaterOrEqual(fit.Scale, 1,
                    $"a note has to be drawn at SOME size, even on {screen.x}x{screen.y}");
                Assert.LessOrEqual(fit.Scale, NotebookKit.MaxScale);
            }
        }

        [Test]
        public void TheNote_StaysInsideItsCornerBudget_AtEveryShippedResolution()
        {
            foreach (Vector2Int screen in Screens())
            {
                QuestPanelFit fit = QuestPanelLayout.Fit(3, screen.x, screen.y);
                if (fit.Scale == 1) continue;   // the fallback is allowed to exceed; see the Fit doc

                Assert.LessOrEqual(fit.ScreenWidthPx, screen.x * QuestPanelLayout.RoomWidthFraction,
                    $"the note is over its width budget at {screen.x}x{screen.y}");
                Assert.LessOrEqual(fit.ScreenHeightPx, screen.y * QuestPanelLayout.RoomHeightFraction,
                    $"the note is over its height budget at {screen.x}x{screen.y}");
            }
        }

        [Test]
        public void ABiggerScreen_NeverShrinksTheNote()
        {
            // Monotonic: the note grows with the room it is given. A ladder that went backwards would
            // read as a bug the first time someone maximised the window.
            int previous = 0;
            foreach (Vector2Int screen in Screens())
            {
                int scale = QuestPanelLayout.Fit(3, screen.x, screen.y).Scale;
                Assert.GreaterOrEqual(scale, previous,
                    $"the note shrank going up to {screen.x}x{screen.y}");
                previous = scale;
            }
        }

        [Test]
        public void MoreLines_MakeATallerNote_NotAWiderOne()
        {
            QuestPanelFit one = QuestPanelLayout.Fit(1, 1920, 1080);
            QuestPanelFit three = QuestPanelLayout.Fit(3, 1920, 1080);

            Assert.AreEqual(one.Cols, three.Cols, "the note's width is fixed; only its height follows the text");
            Assert.Greater(three.HeightPx, one.HeightPx);
        }

        [Test]
        public void OverLongText_IsCappedAndReported_NeverSilentlyTrimmed()
        {
            string tooLong = new string('x', QuestPanelLayout.Cols * (QuestPanelLayout.MaxLines + 2));

            List<string> rows = QuestPanelLayout.Rows(tooLong);
            QuestPanelFit fit = QuestPanelLayout.Fit(
                NotebookLayout.Wrap(tooLong, QuestPanelLayout.Cols).Count, 1280, 720);

            Assert.AreEqual(QuestPanelLayout.MaxLines, rows.Count, "the note caps at its line limit");
            Assert.IsTrue(fit.Overflowed, "and SAYS it dropped rows, rather than trimming in silence");
        }

        // ---- the corner ---------------------------------------------------------------------------

        [Test]
        public void TheNote_IsPinnedIntoTheLowerRightCorner()
        {
            QuestPanelFit fit = QuestPanelLayout.Fit(3, 1280, 720);
            Vector2 pos = QuestPanelLayout.AnchoredPosition(in fit);

            // Anchored and pivoted lower-right by the presenter, so the offset must go LEFT (negative x)
            // and UP (positive y) to inset the note from the corner it hangs off.
            Assert.Less(pos.x, 0f, "the note insets leftward from the right edge");
            Assert.Greater(pos.y, 0f, "the note insets upward from the bottom edge");
        }

        [Test]
        public void TheMargin_ScalesWithTheNote()
        {
            // Eight of the note's OWN pixels at every scale — otherwise the inset shrinks to nothing on
            // a big screen and the note reads as stuck to the rim.
            QuestPanelFit small = QuestPanelLayout.Fit(3, 1280, 720);
            QuestPanelFit large = QuestPanelLayout.Fit(3, 3840, 2160);

            Assert.AreEqual(-QuestPanelLayout.MarginPx * small.Scale,
                            QuestPanelLayout.AnchoredPosition(in small).x, 0.001f);
            Assert.AreEqual(-QuestPanelLayout.MarginPx * large.Scale,
                            QuestPanelLayout.AnchoredPosition(in large).x, 0.001f);
        }

        // ---- fixtures -----------------------------------------------------------------------------

        /// <summary>
        /// Every line the director can put on the note. Listed rather than reflected so a new nudge has
        /// to be added here deliberately — which is the moment to check it fits.
        /// </summary>
        private static IEnumerable<string> EveryShippedNudge() => new[]
        {
            WorldStrings.OnboardTalkGinny,
            WorldStrings.OnboardDigClams,
            WorldStrings.OnboardCrossBar,
            WorldStrings.OnboardBuyLicence,
            WorldStrings.OnboardBuyRod,
            WorldStrings.OnboardBuyDory,
            WorldStrings.OnboardRepairDory,
            WorldStrings.OnboardSailHome,
            WorldStrings.OnboardDone,
        };

        /// <summary>Smallest supported window up to 4K, in increasing order — the ladder the monotonic
        /// test walks.</summary>
        private static IEnumerable<Vector2Int> Screens() => new[]
        {
            new Vector2Int(1280, 720),
            new Vector2Int(1600, 900),
            new Vector2Int(1920, 1080),
            new Vector2Int(2560, 1440),
            new Vector2Int(3840, 2160),
        };
    }
}
