using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using HiddenHarbours.Tools.RigBaking;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.RigBaking.EditMode
{
    /// <summary>
    /// <b>The notebook kit's numbers, diffed against the kit's own generated contract.</b> No V8 and
    /// no Unity assets — pure state, so it runs anywhere.
    ///
    /// <para>The layout formulae are checked by REGENERATION, never by reading: every row of the
    /// kit's 20-row ladder is recomputed from <see cref="NotebookKit"/>'s functions across the whole
    /// legal range (16–40 columns, 6–22 lines). A formula that agrees at 20 points is the same
    /// formula, not a transcription of one.</para>
    /// </summary>
    public class NotebookKitContractTests
    {
        static NotebookKitContract Contract() => NotebookKitContract.Load();

        [Test]
        public void CSharpConstantsAgreeWithTheGeneratedContract()
        {
            var drift = Contract().Disagreements();
            Assert.That(drift, Is.Empty,
                "NotebookKit has drifted from the kit's generated contract. The JSON is the oracle " +
                "(it is written by the rig at its own defaults) — fix the C# constants:\n  " +
                string.Join("\n  ", drift));
        }

        [Test]
        public void TheLadderIsNotEmpty()
        {
            // Guards the guard: Disagreements() regenerates the ladder, and a ladder of zero rows
            // would make that check pass while proving nothing. This is the vacuous-green tripwire
            // for the oracle itself.
            var ladder = Contract().Data.type.ladder;
            Assert.That(ladder, Is.Not.Null.And.Length.EqualTo(20),
                "The contract's type.ladder should carry 20 rows. If the kit legitimately changed " +
                "shape, update this count deliberately — do not let it drift to zero, or the " +
                "formula check silently stops checking anything.");
        }

        [Test]
        public void EveryLadderRowSpansTheLegalRange()
        {
            var ladder = Contract().Data.type.ladder;
            Assert.Multiple(() =>
            {
                Assert.That(ladder.Min(r => r.cols), Is.EqualTo(NotebookKit.MinCols));
                Assert.That(ladder.Max(r => r.cols), Is.EqualTo(NotebookKit.MaxCols));
                Assert.That(ladder.Min(r => r.lines), Is.EqualTo(NotebookKit.MinLines));
                Assert.That(ladder.Max(r => r.lines), Is.EqualTo(NotebookKit.MaxLines));
            });
        }

        [Test]
        public void TheSpreadFormulaeStepByTenPixels()
        {
            // The kit states it as a law: "one more line = +10 px of height; one more column =
            // +10 px of width (both leaves)". A formula that drifts off the 10 px lattice puts the
            // ruled lines off the cell grid, which is the one thing the whole layout rests on.
            for (int c = NotebookKit.MinCols; c < NotebookKit.MaxCols; c++)
                Assert.That(NotebookKit.SpreadWidthFor(c + 1) - NotebookKit.SpreadWidthFor(c),
                    Is.EqualTo(10), $"width step at {c} columns");

            for (int l = NotebookKit.MinLines; l < NotebookKit.MaxLines; l++)
                Assert.That(NotebookKit.SpreadHeightFor(l + 1) - NotebookKit.SpreadHeightFor(l),
                    Is.EqualTo(10), $"height step at {l} lines");
        }

        [Test]
        public void TitleColsIsExactlyTwoCellsWiderThanASteps()
        {
            // Not a tuned number: the box lane is 10 px = two 5 px cells, and a title runs the text
            // lane AND the box lane because nothing is checked off beside it.
            Assert.That(NotebookKit.BoxLane, Is.EqualTo(2 * NotebookKit.CellWidth),
                "the box lane must stay exactly two cells, or titleCols = cols + 2 stops being true");

            for (int c = NotebookKit.MinCols; c <= NotebookKit.MaxCols; c++)
                Assert.That(NotebookKit.TitleColsFor(c), Is.EqualTo(c + 2));
        }

        [Test]
        public void TheClosestTierIsTheOneTheWritingLaneWasGiven()
        {
            // The kit publishes this so the presenter chooses the occupancy knowingly: at the closest
            // tier (4× on a 480×270 screen) the book is 410×232 = 73.4% of the screen.
            Assert.Multiple(() =>
            {
                Assert.That(NotebookKit.SpreadWidthFor(NotebookKit.ClosestTierCols), Is.EqualTo(410));
                Assert.That(NotebookKit.SpreadHeightFor(NotebookKit.ClosestTierLines), Is.EqualTo(232));
                Assert.That(NotebookKit.StepCharsAtClosestTier, Is.EqualTo(31),
                    "31 is the number the writing lane was told to write to");
            });
        }

        [Test]
        public void TheFaceIsTheBubbleKitsAndIsNotRedeclared()
        {
            // Compile-time aliasing is the real enforcement (NotebookKit.CellWidth IS
            // DialogueBubbleKit.CellWidth); this states the intent so a future edit that replaces the
            // alias with a literal is caught by a failing test rather than by a player.
            Assert.Multiple(() =>
            {
                Assert.That(NotebookKit.CellWidth, Is.EqualTo(DialogueBubbleKit.CellWidth));
                Assert.That(NotebookKit.CellHeight, Is.EqualTo(DialogueBubbleKit.CellHeight));
                Assert.That(NotebookKit.GlyphWidth, Is.EqualTo(DialogueBubbleKit.GlyphWidth));
                Assert.That(NotebookKit.GlyphHeight, Is.EqualTo(DialogueBubbleKit.GlyphHeight));
                Assert.That(NotebookKit.BaselineRow, Is.EqualTo(DialogueBubbleKit.BaselineRow));
            });
        }

        [Test]
        public void EveryBakedPieceHasARowInTheKitsReferenceSheet()
        {
            var contract = Contract();
            var orphans = NotebookKit.Pieces
                .Where(p => contract.SheetPiece(NotebookKitContract.SheetKeyFor(p)) == null)
                .ToList();

            Assert.That(orphans, Is.Empty,
                "A baked piece with no sheet row has nothing to check its size against:\n  " +
                string.Join("\n  ", orphans.Select(p => $"{p} → {NotebookKitContract.SheetKeyFor(p)}")));
        }

        [Test]
        public void TheExpressionTableAndThePieceListAreTheSameList()
        {
            // They are one list in two places on purpose. A piece in one and not the other bakes and
            // never imports, or imports and never bakes.
            var exprNames = NotebookKitBaker.PieceExpressions().Select(e => e.Name).ToArray();
            Assert.That(exprNames, Is.EquivalentTo(NotebookKit.Pieces));
            Assert.That(exprNames, Is.Unique);
        }

        [Test]
        public void NoPageOrTypeCompositeIsBaked()
        {
            // The kit is explicit: "NOTHING in this kit contains a word of game text". The page.* and
            // turn.* sheet entries are drawn from NotebookKit.SAMPLE, so baking one would ship sample
            // dialogue as an art asset. The type.* specimens belong to the font, not to a sprite.
            var forbidden = NotebookKit.Pieces
                .Select(NotebookKitContract.SheetKeyFor)
                .Where(k => k.StartsWith("page.") || k.StartsWith("turn.") || k.StartsWith("type."))
                .ToList();

            Assert.That(forbidden, Is.Empty,
                "These bake sample game text or type specimens into sprites: " + string.Join(", ", forbidden));
        }

        [Test]
        public void SheetKeysAreDerivedNotTabulated()
        {
            Assert.Multiple(() =>
            {
                Assert.That(NotebookKitContract.SheetKeyFor("Notebook_Box_Pencil"), Is.EqualTo("box.pencil"));
                Assert.That(NotebookKitContract.SheetKeyFor("Notebook_Stock_Ruled"), Is.EqualTo("stock.ruled"));
                Assert.That(NotebookKitContract.SheetKeyFor("Notebook_Caret"), Is.EqualTo("caret"));
                Assert.That(NotebookKitContract.SheetKeyFor("Notebook_Dogear"), Is.EqualTo("dogear"));
                Assert.That(NotebookKitContract.SheetKeyFor("Notebook_Select_Pill"), Is.EqualTo("select.pill"));
            });
        }

        // ---- the kit's bundled copies must not fork from the shipping rigs ----------------------

        [Test]
        public void TheKitsHarnessCopiesAreByteIdenticalToTheShippingRigs()
        {
            // The kit ships its own copies of dialogueBubbleRig.js and isoSolid.js so harness.html
            // opens standalone. The CATALOG points at the shipping files instead — pointing at a
            // duplicate forks the rig the day either copy moves. This keeps the duplicates honest,
            // and is the same guard #561 put on the character rigs.
            var pairs = new (string Kit, string Shipping)[]
            {
                ("docs/art/rigs/notebook-kit/Art/dialogueBubbleRig.js",
                 "docs/art/rigs/dialogue-bubble-kit/Art/dialogueBubbleRig.js"),
                ("docs/art/rigs/notebook-kit/Art/isoSolid.js",
                 "docs/art/rigs/deck-loop-kit/Art/isoSolid.js"),
            };

            foreach (var (kit, shipping) in pairs)
            {
                string a = Path.Combine(RigCatalog.RepoRoot, kit);
                string b = Path.Combine(RigCatalog.RepoRoot, shipping);
                Assert.That(File.Exists(a), $"{kit} is missing");
                Assert.That(File.Exists(b), $"{shipping} is missing");
                Assert.That(File.ReadAllBytes(a), Is.EqualTo(File.ReadAllBytes(b)),
                    $"{kit} has forked from {shipping}. Re-sync them, or the harness and the game " +
                    "are drawing from two different rigs.");
            }
        }

        [Test]
        public void TheCatalogPointsAtTheShippingFaceNotTheKitsCopy()
        {
            var entry = RigCatalog.Get(NotebookKitBaker.RigKey);
            Assert.That(entry.Prerequisites, Does.Contain("dialogueBubble"),
                "The notebook ships no face of its own — without the bubble rig there is nothing to " +
                "set the page in.");
            Assert.That(entry.Prerequisites, Does.Contain("deckIsoSolid"),
                "Without isoSolid the rig still loads and the furniture still draws, but " +
                "NotebookIsoKit silently never registers — see RigCatalog.NotebookKit.cs.");
        }
    }
}
