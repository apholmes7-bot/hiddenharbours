using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using HiddenHarbours.World;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// <b>THE BOOK'S LAYOUT, PROVEN HEADLESSLY.</b> No canvas, no camera, no art — which is the whole
    /// reason the flow lives in a POCO. What is pinned here is the part that would fail INVISIBLY: a
    /// line quietly clipped, a quest quietly dropped off the end, a page that stops at the last ruled
    /// line and never says there is more.
    /// </summary>
    public class NotebookLayoutTests
    {
        // ---- fit ------------------------------------------------------------------------------------

        /// <summary>
        /// <b>The kit's own tier table, reproduced by <see cref="NotebookLayout.Fit"/>.</b>
        ///
        /// <para>These three rows are copied from the kit's published tiers — the room each zoom tier
        /// leaves and the book it affords. They are the diff the handoff asks for: our formula against
        /// the rig's table, not our formula against itself. A change to <see cref="NotebookLayout.Fit"/>
        /// that no longer lands on the kit's answer is a change to what the player sees.</para>
        /// </summary>
        [TestCase(825, 464, 3, 17, 12, TestName = "Fit_zoom2_tier")]
        [TestCase(550, 309, 2, 17, 12, TestName = "Fit_zoom3_tier")]
        [TestCase(412, 232, 1, 31, 20, TestName = "Fit_zoom4_closest_tier")]
        public void FitReproducesTheKitsTierTable(int roomW, int roomH, int scale, int cols, int lines)
        {
            NotebookFit fit = NotebookLayout.Fit(roomW, roomH);

            Assert.That(fit.Clamped, Is.False, "the kit's own rooms all hold a legal book");
            Assert.That(fit.Scale, Is.EqualTo(scale), "scale");
            Assert.That(fit.Cols, Is.EqualTo(cols), "cols");
            Assert.That(fit.Lines, Is.EqualTo(lines), "lines");
        }

        [Test]
        public void TheFittedBookActuallyFitsTheRoomItWasGiven()
        {
            // The property behind the table: whatever Fit returns must be drawable in the room. A
            // clamp that quietly overhung would look exactly like a book that fitted.
            for (int w = 260; w <= 1600; w += 37)
            {
                for (int h = 92; h <= 900; h += 41)
                {
                    NotebookFit fit = NotebookLayout.Fit(w, h);
                    if (fit.Clamped) continue;

                    Assert.That(fit.SpreadWidth, Is.LessThanOrEqualTo(w),
                        $"a {fit.Cols}×{fit.Lines} book at scale {fit.Scale} overhangs a {w}×{h} room");
                    Assert.That(fit.SpreadHeight, Is.LessThanOrEqualTo(h),
                        $"a {fit.Cols}×{fit.Lines} book at scale {fit.Scale} overhangs a {w}×{h} room");
                }
            }
        }

        [Test]
        public void ARoomTooSmallForAnyLegalBookSaysSoRatherThanShrinking()
        {
            // The kit's instruction: never clamp a returned rect yourself, read `clamped` — or the
            // flag becomes a lie about what is on screen.
            NotebookFit fit = NotebookLayout.Fit(10, 10);

            Assert.That(fit.Clamped, Is.True);
            Assert.That(fit.Cols, Is.EqualTo(NotebookKit.MinCols));
            Assert.That(fit.Lines, Is.EqualTo(NotebookKit.MinLines));
        }

        // ---- wrap ------------------------------------------------------------------------------------

        [Test]
        public void WrapKeepsEveryWordRatherThanClipping()
        {
            const string line = "Row out past the point and set the string before the tide turns back";
            var wrapped = NotebookLayout.Wrap(line, 31);

            Assert.That(wrapped.Count, Is.GreaterThan(1), "this line is longer than 31 characters");
            foreach (string w in wrapped)
                Assert.That(w.Length, Is.LessThanOrEqualTo(31), $"'{w}' overhangs the margin rule");

            // Every word survives, in order — the copy budget is advice to the writing lane, not a
            // limit the layout enforces with scissors.
            var wordsIn = line.Split(' ');
            var wordsOut = string.Join(" ", wrapped).Split(' ');
            Assert.That(wordsOut, Is.EqualTo(wordsIn));
        }

        [Test]
        public void AWordLongerThanTheLineIsHardBrokenRatherThanOverhanging()
        {
            var wrapped = NotebookLayout.Wrap(new string('m', 40), 16);

            Assert.That(wrapped.Count, Is.EqualTo(3), "40 m's at 16 columns is three lines");
            foreach (string w in wrapped) Assert.That(w.Length, Is.LessThanOrEqualTo(16));
            Assert.That(string.Concat(wrapped).Length, Is.EqualTo(40), "no character was dropped");
        }

        [Test]
        public void AnEmptyLineIsStillALine()
        {
            // A blank note row must occupy a ruled line, or her list closes up around it.
            Assert.That(NotebookLayout.Wrap("", 20).Count, Is.EqualTo(1));
        }

        // ---- blocks ------------------------------------------------------------------------------------

        [Test]
        public void AStepsBoxIsDrawnOnceEvenWhenTheStepWraps()
        {
            var steps = new List<(string, NotebookStepState)>
            {
                (new string('x', 70), NotebookStepState.Todo),
            };
            NotebookBlock block = NotebookLayout.Quest("T", null, steps, 16);

            var boxed = block.Rows.Where(r => r.HasBox).ToList();
            Assert.That(boxed.Count, Is.EqualTo(1), "a wrapped step that re-boxed itself reads as two steps");
            Assert.That(block.Rows.Count(r => r.Continuation), Is.GreaterThan(0));
        }

        [Test]
        public void ATitleRunsTheWideLaneAndAStepDoesNot()
        {
            var steps = new List<(string, NotebookStepState)> { ("step", NotebookStepState.Todo) };
            NotebookBlock block = NotebookLayout.Quest("title", null, steps, 20);

            Assert.That(block.Rows[0].Wide, Is.True, "the title runs the text lane AND the box lane");
            Assert.That(block.Rows.Last().Wide, Is.False, "a step leaves the box lane to the box");
        }

        [Test]
        public void AProseNoteIsWideAndATaskNoteCarriesTheBox()
        {
            Assert.That(NotebookLayout.Note("just a thought", isTask: false, done: false, 20).Rows[0].Wide,
                        Is.True, "prose runs the full title width");
            Assert.That(NotebookLayout.Note("do the thing", isTask: true, done: false, 20).Rows[0].HasBox,
                        Is.True, "a task carries the shared checkbox");
        }

        // ---- the flow ------------------------------------------------------------------------------------

        static NotebookBlock Filler(int rows, string id = "b")
        {
            var list = new List<NotebookRow>();
            for (int i = 0; i < rows; i++)
                list.Add(new NotebookRow($"{id}.{i}", false, false, NotebookStepState.Todo, false));
            return new NotebookBlock(NotebookBlockKind.Section, list, id);
        }

        static int RowsPlaced(IEnumerable<NotebookSpread> spreads) =>
            spreads.Sum(s => s.Left.Placements.Sum(p => p.RowCount) + s.Right.Placements.Sum(p => p.RowCount));

        [Test]
        public void NothingIsEverDropped()
        {
            // ⭐ The property that matters most. A silent cap here reads on screen exactly like a short
            // list, so it could ship and never be noticed.
            var blocks = new List<NotebookBlock>();
            for (int i = 0; i < 40; i++) blocks.Add(Filler(1 + i % 7, "b" + i));

            int want = blocks.Sum(b => b.Height);
            var spreads = NotebookLayout.Paginate(blocks, cols: 31, lines: 20);

            Assert.That(RowsPlaced(spreads), Is.EqualTo(want),
                "rows went in that never came out — the flow truncated instead of turning the page");
        }

        [Test]
        public void OverflowIsMorePagesAndNeverAClippedOne()
        {
            // The sabotage arm the handoff asks for: fill a spread exactly, then add one more row.
            // The answer must be a SECOND spread, never a row quietly dropped off the last line.
            const int lines = 10;
            var full = new List<NotebookBlock> { Filler(lines, "L"), Filler(lines, "R") };

            var exact = NotebookLayout.Paginate(full, cols: 20, lines: lines);
            Assert.That(exact.Count, Is.EqualTo(1), "two full leaves are exactly one spread");

            full.Add(Filler(1, "one-more"));
            var over = NotebookLayout.Paginate(full, cols: 20, lines: lines);

            Assert.That(over.Count, Is.EqualTo(2), "one row past a full spread is a page turn");
            Assert.That(RowsPlaced(over), Is.EqualTo(2 * lines + 1));
            Assert.That(over[1].Left.Placements[0].Block.SourceId, Is.EqualTo("one-more"));
        }

        [Test]
        public void NoLeafIsEverOverfilled()
        {
            var blocks = new List<NotebookBlock>();
            for (int i = 0; i < 25; i++) blocks.Add(Filler(3 + i % 5, "b" + i));

            const int lines = 12;
            foreach (NotebookSpread spread in NotebookLayout.Paginate(blocks, cols: 24, lines: lines))
            {
                Assert.That(spread.Left.Used, Is.LessThanOrEqualTo(lines));
                Assert.That(spread.Right.Used, Is.LessThanOrEqualTo(lines));
            }
        }

        [Test]
        public void ABlockThatWouldFitAnEmptyLeafMovesWholeRatherThanSplitting()
        {
            // The kit's rule, and the failure it exists to prevent: a quest's title on one leaf and
            // its steps on the other.
            const int lines = 10;
            var blocks = new List<NotebookBlock> { Filler(7, "first"), Filler(6, "second") };

            var spreads = NotebookLayout.Paginate(blocks, cols: 20, lines: lines);

            Assert.That(spreads.Count, Is.EqualTo(1));
            Assert.That(spreads[0].Left.Placements.Count, Is.EqualTo(1), "only the first block fits the left leaf");
            Assert.That(spreads[0].Right.Placements.Count, Is.EqualTo(1));
            Assert.That(spreads[0].Right.Placements[0].IsSplit, Is.False,
                "a block that fits an empty leaf must never be split");
        }

        [Test]
        public void ABlockTallerThanAWholeLeafIsSplitRatherThanDropped()
        {
            // The only split the kit permits — "because the alternative is not drawing it".
            const int lines = 6;
            var blocks = new List<NotebookBlock> { Filler(15, "long") };

            var spreads = NotebookLayout.Paginate(blocks, cols: 16, lines: lines);

            Assert.That(RowsPlaced(spreads), Is.EqualTo(15), "every row of the long block is drawn somewhere");
            Assert.That(spreads[0].Left.Placements[0].IsSplit, Is.True);
        }

        [Test]
        public void AnEmptyBookIsOneEmptySpreadRatherThanNoSpreads()
        {
            // The presenter draws Spreads[SpreadIndex]; zero spreads would be a page that cannot be
            // drawn at all rather than an empty one.
            var spreads = NotebookLayout.Paginate(new List<NotebookBlock>(), cols: 20, lines: 10);

            Assert.That(spreads.Count, Is.EqualTo(1));
            Assert.That(NotebookLayout.RowsOn(spreads[0]), Is.EqualTo(0));
        }
    }
}
