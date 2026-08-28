using NUnit.Framework;
using HiddenHarbours.World;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// The kit's arithmetic, as a POCO fixture: which tail a placement chooses, and the panel/chip
    /// formulae the presenter sizes itself with.
    ///
    /// <para><b>State, not pixels, and no key presses.</b> Headless CI cannot read a framebuffer and
    /// cannot deliver an input event, so everything worth asserting about the dressed bubble has to
    /// be reachable as a value. That is why the tail choice lives in
    /// <see cref="DialogueBubbleKit.TailIndexFor"/> rather than inside the presenter's
    /// <c>PlaceTail</c> — the same split that made <c>DialogueBubbleLayout</c> testable.</para>
    /// </summary>
    public class DialogueBubbleKitTests
    {
        const float Scale = DialogueBubbleKit.ArtScale;

        // A 22-column panel, the kit's own worked width, at the shipped art scale.
        static float PanelWidth => DialogueBubbleKit.PanelWidthFor(22) * Scale;

        [Test]
        public void ASpeakerUnderTheMiddleTakesTheCentreTail()
        {
            int i = DialogueBubbleKit.TailIndexFor(true, PanelWidth * 0.5f, 0f, PanelWidth, Scale);
            Assert.That(DialogueBubbleKit.TailPieces[i], Is.EqualTo(DialogueBubbleKit.PieceTailCentre));
        }

        [Test]
        public void ASpeakerUnderTheLeftEdgeTakesTheLeftTail()
        {
            int i = DialogueBubbleKit.TailIndexFor(true, 0f, 0f, PanelWidth, Scale);
            Assert.That(DialogueBubbleKit.TailPieces[i], Is.EqualTo(DialogueBubbleKit.PieceTailLeft));
        }

        [Test]
        public void ASpeakerUnderTheRightEdgeTakesTheRightTail()
        {
            int i = DialogueBubbleKit.TailIndexFor(true, PanelWidth, 0f, PanelWidth, Scale);
            Assert.That(DialogueBubbleKit.TailPieces[i], Is.EqualTo(DialogueBubbleKit.PieceTailRight));
        }

        [Test]
        public void ABubbleThatFlippedUnderTheSpeakerTakesTheUpSet_NotAFlippedDownTail()
        {
            // The whole reason the kit ships six drawn tails instead of three: the up set is
            // measured, so nothing here derives a tip by flipping one.
            for (int third = 0; third < 3; third++)
            {
                float x = third == 0 ? 0f : third == 1 ? PanelWidth * 0.5f : PanelWidth;

                int down = DialogueBubbleKit.TailIndexFor(true, x, 0f, PanelWidth, Scale);
                int up = DialogueBubbleKit.TailIndexFor(false, x, 0f, PanelWidth, Scale);

                Assert.That(up, Is.EqualTo(down + DialogueBubbleKit.FirstUpTail),
                            "the up tail is the same third, in the up set");
                Assert.That(up, Is.GreaterThanOrEqualTo(DialogueBubbleKit.FirstUpTail));
                Assert.That(DialogueBubbleKit.TailTipY[up],
                            Is.Not.EqualTo(DialogueBubbleKit.TailTipY[down]));
            }
        }

        [Test]
        public void TheChoiceIsOffsetInvariant_ABubbleThatSlidStillPointsTheSameWay()
        {
            // The bubble slides to stay on screen; the tail keeps pointing at the speaker. So the
            // answer must depend on where the tail root sits WITHIN the panel, not on where the
            // panel happens to be.
            const float offset = 733f;
            for (int third = 0; third < 3; third++)
            {
                float local = third == 0 ? 0f : third == 1 ? PanelWidth * 0.5f : PanelWidth;

                Assert.That(DialogueBubbleKit.TailIndexFor(true, offset + local, offset, PanelWidth, Scale),
                            Is.EqualTo(DialogueBubbleKit.TailIndexFor(true, local, 0f, PanelWidth, Scale)));
            }
        }

        [Test]
        public void TheNarrowestPanelStillResolvesAllThreeThirds()
        {
            // At the kit's 8-column minimum the panel is 49 px, and twice the 8 px tail inset is 16 —
            // comfortably inside it. But the guard matters: if the inset ever exceeded the half
            // width, "left" would sit right of "centre" and the nearest-of-three answer would be
            // nonsense rather than merely approximate.
            float narrow = DialogueBubbleKit.PanelWidthFor(DialogueBubbleKit.MinCols) * Scale;

            int left = DialogueBubbleKit.TailIndexFor(true, 0f, 0f, narrow, Scale);
            int centre = DialogueBubbleKit.TailIndexFor(true, narrow * 0.5f, 0f, narrow, Scale);
            int right = DialogueBubbleKit.TailIndexFor(true, narrow, 0f, narrow, Scale);

            Assert.That(left, Is.EqualTo(0));
            Assert.That(centre, Is.EqualTo(1));
            Assert.That(right, Is.EqualTo(2));
        }

        [Test]
        public void EveryIndexIsInRange_ForAbsurdInputs()
        {
            // A speaker far off either side, and a degenerate zero-width panel: the presenter
            // indexes an array with this, so "in range" is not a nicety.
            foreach (bool down in new[] { true, false })
                foreach (float w in new[] { 0f, 1f, PanelWidth })
                    foreach (float x in new[] { -10000f, -1f, 0f, w * 0.5f, w, w + 10000f })
                    {
                        int i = DialogueBubbleKit.TailIndexFor(down, x, 0f, w, Scale);
                        Assert.That(i, Is.InRange(0, DialogueBubbleKit.TailPieces.Length - 1),
                                    $"down={down} w={w} x={x}");
                    }
        }

        [Test]
        public void ThePieceListIsInternallyConsistent()
        {
            Assert.That(DialogueBubbleKit.TailPieces.Length, Is.EqualTo(DialogueBubbleKit.TailKeys.Length));
            Assert.That(DialogueBubbleKit.TailPieces.Length, Is.EqualTo(DialogueBubbleKit.TailHeights.Length));
            Assert.That(DialogueBubbleKit.TailPieces.Length, Is.EqualTo(DialogueBubbleKit.TailTipX.Length));
            Assert.That(DialogueBubbleKit.TailPieces.Length, Is.EqualTo(DialogueBubbleKit.TailTipY.Length));
            Assert.That(DialogueBubbleKit.CursorPieces.Length, Is.EqualTo(DialogueBubbleKit.CursorKeys.Length));

            // Every tail and cursor is in the master list, and the master list has no duplicates.
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (string p in DialogueBubbleKit.Pieces)
                Assert.That(seen.Add(p), Is.True, $"'{p}' appears twice in Pieces");

            foreach (string p in DialogueBubbleKit.TailPieces) Assert.That(seen, Contains.Item(p));
            foreach (string p in DialogueBubbleKit.CursorPieces) Assert.That(seen, Contains.Item(p));
            Assert.That(seen, Contains.Item(DialogueBubbleKit.ShippedCursor));
        }

        [Test]
        public void EveryTipPixelIsInsideItsOwnTail()
        {
            // The tip is the anchor. A tip outside the sprite would put the pivot outside the rect
            // and aim the tail at a point it never touches.
            for (int i = 0; i < DialogueBubbleKit.TailPieces.Length; i++)
            {
                Assert.That(DialogueBubbleKit.TailTipX[i],
                            Is.InRange(0, DialogueBubbleKit.TailWidth - 1));
                Assert.That(DialogueBubbleKit.TailTipY[i],
                            Is.InRange(0, DialogueBubbleKit.TailHeights[i] - 1));
            }
        }

        [Test]
        public void ThePanelLadderRoundTrips()
        {
            // Independent of the contract fixture (which reads the JSON): the two formulae must at
            // least agree with each other, so a panel sized for N columns wraps at N.
            for (int cols = DialogueBubbleKit.MinCols; cols <= DialogueBubbleKit.MaxCols; cols++)
            {
                int panel = DialogueBubbleKit.PanelWidthFor(cols);
                int textBox = panel - DialogueBubbleKit.PanelInsetL - DialogueBubbleKit.PanelInsetR;
                Assert.That(DialogueBubbleKit.ColsFor(textBox), Is.EqualTo(cols),
                            $"a panel sized for {cols} columns must wrap at {cols}");
            }
        }

        [Test]
        public void TheChipGrowsByOneCellPerCharacter()
        {
            for (int chars = 1; chars < 12; chars++)
                Assert.That(DialogueBubbleKit.ChipWidthFor(chars + 1) -
                            DialogueBubbleKit.ChipWidthFor(chars),
                            Is.EqualTo(DialogueBubbleKit.CellWidth));
        }

        [Test]
        public void SpeechIsSentenceCase_AndLabelsAreCaps()
        {
            // The owner's 2026-08-17 ruling, pinned so a later "let's just caps it all" has to argue
            // with a red test rather than with a comment.
            Assert.That(DialogueBubbleKit.BodyIsSentenceCase, Is.True);
            Assert.That(DialogueBubbleKit.LabelsAreCaps, Is.True);

            // And the cell paid for it: mixed case cost +1 advance and +2 line height (4×8 → 5×10).
            Assert.That(DialogueBubbleKit.CellWidth, Is.EqualTo(5));
            Assert.That(DialogueBubbleKit.CellHeight, Is.EqualTo(10));
            Assert.That(DialogueBubbleKit.Descender, Is.GreaterThan(0),
                        "lowercase exists — g j p q y need their two rows");
        }
    }
}
