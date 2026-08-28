using System.IO;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Tools.RigBaking;
using HiddenHarbours.World;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.RigBaking.EditMode
{
    /// <summary>
    /// <b>The kit's generated contract is the oracle, and this is what keeps everything honest
    /// against it.</b>
    ///
    /// <para><see cref="DialogueBubbleKit"/> carries the kit's numbers as C# so the presenter can be
    /// runtime code with no file to read. That copy is only safe because of this file: every
    /// constant is diffed against <c>dialogueBubble.contract.json</c>, which the art lane generates
    /// from the rig and never hand-edits. Nothing here is transcribed — the expectations are READ
    /// FROM THE JSON at test time, so a regenerated contract moves the test with it.</para>
    /// </summary>
    public class BubbleKitContractTests
    {
        BubbleKitContract _contract;

        [SetUp]
        public void Load() => _contract = BubbleKitContract.Load();

        [Test]
        public void TheKitLandedWhereTheImporterLooksForIt()
        {
            string folder = Path.Combine(RigCatalog.RepoRoot, BubbleKitContract.KitFolder);
            Assert.That(Directory.Exists(folder), Is.True,
                        $"the bubble kit is not at {folder}. Every number below is read from it.");
            Assert.That(File.Exists(Path.Combine(folder, BubbleKitContract.ContractFileName)), Is.True);
        }

        [Test]
        public void EveryConstantAgreesWithTheGeneratedContract()
        {
            var drift = _contract.Disagreements();

            // Reported all at once rather than one per run: a regrid moves many numbers together,
            // and a test that stops at the first would take one red-green cycle per value.
            Assert.That(drift, Is.Empty,
                        "DialogueBubbleKit has drifted from the kit's contract:\n  " +
                        string.Join("\n  ", drift) +
                        $"\nThe contract ({_contract.SourcePath}) is generated from the rig and " +
                        "wins. Fix the C#.");
        }

        [Test]
        public void TheGridIs32_TheAssetsGrid_NotTheCameraSide24()
        {
            // The question the armed tripwire told the import PR to settle, settled from the kit's
            // own words rather than by picking one.
            Assert.That(_contract.Data.grid.ppu, Is.EqualTo(32),
                        "the bubble is an object IN the scene, so it is authored on the same grid " +
                        "as the props it stands among. 24 px/m is the camera-side number.");
            Assert.That(DialogueBubbleKit.PixelsPerUnit, Is.EqualTo(_contract.Data.grid.ppu));
        }

        [Test]
        public void ThePanelFormulaeReproduceEveryRowOfTheKitsOwnLadder()
        {
            // The kit publishes a 24-row cols×lines table with the panel px it expects. If our
            // arithmetic is right it regenerates that table exactly — which is a far stronger check
            // than asserting a couple of sizes, and it is the kit's data driving it.
            Assert.That(_contract.Data.type.ladder, Is.Not.Empty);

            foreach (var row in _contract.Data.type.ladder)
            {
                Assert.That(row.panel, Has.Length.EqualTo(2), $"ladder row {row.cols}×{row.lines}");

                Assert.That(DialogueBubbleKit.PanelWidthFor(row.cols), Is.EqualTo(row.panel[0]),
                            $"panel width for {row.cols} cols");
                Assert.That(DialogueBubbleKit.PanelHeightFor(row.lines), Is.EqualTo(row.panel[1]),
                            $"panel height for {row.lines} lines");

                // And the wrap the fill depends on: chars per line, off the text box the kit says
                // that panel has.
                Assert.That(DialogueBubbleKit.ColsFor(row.textBox[0]), Is.EqualTo(row.charsPerLine),
                            $"chars per line for a {row.textBox[0]} px text box");
            }
        }

        [Test]
        public void TheOptionLadderStepsByTheKitsRowHeight()
        {
            foreach (var row in _contract.Data.type.options)
            {
                Assert.That(row.rowH, Is.EqualTo(DialogueBubbleKit.OptionRowHeight));
                Assert.That(row.textX, Is.EqualTo(DialogueBubbleKit.OptionTextX));
                Assert.That(row.textY, Is.EqualTo(DialogueBubbleKit.OptionTextY));
                Assert.That(row.px, Has.Length.EqualTo(2));
            }

            // 2/3/4 rows, +rowH each — the arithmetic the presenter sizes the option bubble with.
            var ladder = _contract.Data.type.options;
            for (int i = 1; i < ladder.Length; i++)
                Assert.That(ladder[i].px[1] - ladder[i - 1].px[1],
                            Is.EqualTo(DialogueBubbleKit.OptionRowHeight),
                            "each extra row costs exactly one row height");
        }

        [Test]
        public void TheSixTailsMatchTheContract_AndTheUpSetIsDrawnNotFlipped()
        {
            Assert.That(_contract.Data.tail.set, Has.Length.EqualTo(DialogueBubbleKit.TailPieces.Length));

            for (int i = 0; i < DialogueBubbleKit.TailKeys.Length; i++)
            {
                var t = _contract.Tail(DialogueBubbleKit.TailKeys[i]);

                Assert.That(t.w, Is.EqualTo(DialogueBubbleKit.TailWidth), $"tail '{t.key}' width");
                Assert.That(t.h, Is.EqualTo(DialogueBubbleKit.TailHeights[i]), $"tail '{t.key}' height");
                Assert.That(t.tip, Has.Length.EqualTo(2));
                Assert.That(t.tip[0], Is.EqualTo(DialogueBubbleKit.TailTipX[i]), $"tail '{t.key}' tip x");
                Assert.That(t.tip[1], Is.EqualTo(DialogueBubbleKit.TailTipY[i]), $"tail '{t.key}' tip y");
            }

            // ⚠️ THE UP SET IS ITS OWN ART. If a down tail and its up partner ever shared a tip y,
            // somebody has "optimised" the up set into a y-flip of the down set — at which point the
            // tip pixel, which IS the anchor, becomes derived rather than measured.
            for (int i = 0; i < DialogueBubbleKit.FirstUpTail; i++)
            {
                int up = i + DialogueBubbleKit.FirstUpTail;
                Assert.That(_contract.Data.tail.set[up].dir, Is.EqualTo("up"));
                Assert.That(_contract.Data.tail.set[i].dir, Is.EqualTo("down"));
                Assert.That(DialogueBubbleKit.TailTipY[up], Is.Not.EqualTo(DialogueBubbleKit.TailTipY[i]),
                            "an up tail tips at the TOP and a down tail at the BOTTOM — equal tip " +
                            "rows mean the up set has been derived by flipping, not measured");
            }
        }

        [Test]
        public void EveryCursorInTheKitBakes_AndTheShippedOneIsTheOwnersPick()
        {
            Assert.That(_contract.Data.cursor, Has.Length.EqualTo(DialogueBubbleKit.CursorPieces.Length),
                        "all four cursors bake — an alternate the owner cannot see costs him the " +
                        "choice the art lane already paid to draw");

            for (int i = 0; i < DialogueBubbleKit.CursorKeys.Length; i++)
                Assert.That(_contract.Cursor(DialogueBubbleKit.CursorKeys[i]), Is.Not.Null);

            // The shipped pick is the one the kit marks, not one this repo chose independently.
            string shipped = null;
            foreach (var c in _contract.Data.cursor) if (c.ship) shipped = c.key;
            Assert.That(shipped, Is.EqualTo("hand"), "the pointing hand is the owner's pick");

            int index = System.Array.IndexOf(DialogueBubbleKit.CursorKeys, shipped);
            Assert.That(DialogueBubbleKit.ShippedCursor,
                        Is.EqualTo(DialogueBubbleKit.CursorPieces[index]));
        }

        [Test]
        public void TheShippedStockAndHighlightAreTheOnesTheKitMarks()
        {
            string stock = null;
            foreach (var s in _contract.Data.stocks) if (s.ship) stock = s.key;
            Assert.That(stock, Is.EqualTo(DialogueBubbleKit.ShippedStock));

            string highlight = null;
            foreach (var h in _contract.Data.options.highlight) if (h.ship) highlight = h.key;
            Assert.That(highlight, Is.EqualTo(DialogueBubbleKit.ShippedHighlight));
        }

        [Test]
        public void TheOcclusionInvariantHolds_AtEveryTail()
        {
            // The kit proves this itself and publishes the verdict: the speech panel, name chip and
            // option bubble never occlude the speaker, at all six tails, worst case, priced against
            // the closest zoom tier. We assert the proof came out true rather than re-deriving it —
            // re-deriving it here would be a second implementation to keep in step.
            Assert.That(_contract.Data.occupancy.holds, Is.True,
                        "the kit's own screenBudget() says the invariant does NOT hold. The " +
                        "character IS the portrait; nothing may cover her.");
            Assert.That(_contract.Data.occupancy.gap, Is.EqualTo(DialogueBubbleKit.TailGap),
                        "the tail is the deliberate exception and stops this far clear of her ink");
        }

        [Test]
        public void TheMarkerBakesATallerCellThanItDraws_SoTheBobRegisters()
        {
            // The trap this kit sets: render('marker') allocates h + 1 so both bob frames share one
            // cell. Cut them to their own ink extents and the bob gains a second pixel of travel.
            Assert.That(DialogueBubbleKit.MarkerCellHeight,
                        Is.EqualTo(_contract.Data.marker.h + 1));
            Assert.That(_contract.Data.marker.bob, Is.EqualTo(new[] { 0, 1 }));
        }
    }

    /// <summary>
    /// The border table, pinned on both sides of the assembly wall.
    ///
    /// <para><c>DialogueBubbleArtTests</c> (in the World test assembly) cannot see
    /// <see cref="BubbleKitImporter"/>, so it computes the expected border itself from
    /// <see cref="DialogueBubbleKit"/>. This asserts the importer computes the SAME thing from the
    /// same constants, so the two copies cannot drift apart.</para>
    /// </summary>
    public class BubbleKitImporterBorderTests
    {
        [Test]
        public void ThePanelAndGoldTilesSliceOnAllFourSides_EverythingElseIsAFixedStamp()
        {
            int p = DialogueBubbleKit.PanelCorner;
            Assert.That(BubbleKitImporter.BorderFor(DialogueBubbleKit.PiecePanel),
                        Is.EqualTo(new Vector4(p, p, p, p)));

            int g = DialogueBubbleKit.GoldCorner;
            Assert.That(BubbleKitImporter.BorderFor(DialogueBubbleKit.PieceGold),
                        Is.EqualTo(new Vector4(g, g, g, g)));

            foreach (string piece in DialogueBubbleKit.Pieces)
            {
                if (piece == DialogueBubbleKit.PiecePanel || piece == DialogueBubbleKit.PieceGold)
                    continue;

                Assert.That(BubbleKitImporter.BorderFor(piece), Is.EqualTo(Vector4.zero),
                            $"'{piece}' is a fixed stamp. A border here would let Unity stretch it " +
                            "— and on a tail that means stretching the tip pixel, which is the " +
                            "anchor the whole placement is built on.");
            }
        }

        [Test]
        public void TheGoldTileIsBigEnoughToCarryItsOwnVocabulary()
        {
            // A chamfer-1 panel is cap · light · paper · shade · cap. The tile must hold all five
            // rows and still have a stretchable middle, or 9-slicing it loses a band.
            Assert.That(DialogueBubbleKit.GoldTileSize,
                        Is.GreaterThanOrEqualTo(DialogueBubbleKit.GoldCorner * 2 + 1));
        }

        [Test]
        public void EveryPieceHasAnImportPath_AndTheyAreAllDistinct()
        {
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (string piece in DialogueBubbleKit.Pieces)
            {
                string path = BubbleKitImporter.AssetPathFor(piece);
                Assert.That(path, Does.StartWith(DialoguePresenter.ArtFolder));
                Assert.That(seen.Add(path), Is.True, $"two pieces claim '{path}'");
            }

            Assert.That(seen, Has.Count.EqualTo(DialogueBubbleKit.Pieces.Length));
        }
    }
}
