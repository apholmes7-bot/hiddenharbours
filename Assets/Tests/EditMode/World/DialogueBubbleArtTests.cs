using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.World;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// 🟢 <b>THE TRIPWIRE, FLIPPED.</b> This file used to assert that the bubble kit had NOT landed,
    /// so that its landing would be an event somebody saw. It has landed, and its own failure message
    /// said what to do next. This is that.
    ///
    /// <para><b>What it does now — and the reverse tripwire.</b> It pins the baked path: every piece
    /// resolves, at the size and with the 9-slice border the kit declares. Which means it now fails
    /// the day the art LEAVES — a deleted folder, a renamed piece, a re-bake that quietly drops a
    /// tail — instead of the day it arrived. Same principle, opposite direction: the thing that must
    /// not silently change is now "the art is here and correct" rather than "the art is absent".</para>
    ///
    /// <para><b>⚠️ IT CANNOT PASS VACUOUSLY, AND THAT IS THE POINT.</b> The obvious way to write this
    /// is to skip when the folder is missing — and that is precisely the vacuous green the cloud-lane
    /// protocol forbids, because a test that passes when the art is absent is a test that would have
    /// passed for the last six months of it being absent. So the existence of the pieces is not a
    /// GUARD on the assertions, it IS the first assertion. Before the bake has been run this file is
    /// RED and says which menu item to run; after it, it pins the result. There is no third state
    /// where it is green and checking nothing.</para>
    ///
    /// <para><b>Where the numbers come from.</b> This assembly cannot see the editor-side contract
    /// reader (<c>HiddenHarbours.Tools.RigBaking.Editor</c> is not one of its references), so the
    /// borders here are checked against <see cref="DialogueBubbleKit"/>. That is not a transcription
    /// with a second life: <c>BubbleKitContractTests</c> diffs every one of those constants against
    /// the kit's generated JSON, so the chain is contract → constants → these assertions, with a test
    /// on each link.</para>
    /// </summary>
    public class DialogueBubbleArtTests
    {
        /// <summary>What to do when this file is red because nothing has been baked yet. Spelled out
        /// once so every assertion can say it.</summary>
        const string RunTheBake =
            "\n\n🔴 THE BUBBLE KIT HAS NOT BEEN BAKED IN THIS CHECKOUT.\n" +
            "  This is EXPECTED on a fresh clone and in any run that has not baked: the kit ships " +
            "NO PNGs — it is all rig source — so the sprites are produced, not committed by the art " +
            "lane.\n" +
            "  Fix: run  Hidden Harbours ▸ Art ▸ Bake Dialogue Bubble Kit  (or, headless, " +
            "-executeMethod HiddenHarbours.Tools.RigBaking.BubbleKitBakeMenu.BakeAndImportHeadless " +
            "— ⚠ twice on a fresh worktree, because a first Unity run can import only and never " +
            "call the method), then commit the PNGs and their .meta files.\n" +
            "  This test is deliberately NOT skipped when the art is missing. A test that passes " +
            "when the art is absent is a test that proves nothing, and this file exists to stop " +
            "exactly that.";

        static string PathFor(string piece) => $"{DialoguePresenter.ArtFolder}/{piece}.png";

        [Test]
        public void TheKitIsBaked_EveryPieceResolves()
        {
            Assert.That(Directory.Exists(DialoguePresenter.ArtFolder), Is.True,
                        $"{DialoguePresenter.ArtFolder} does not exist.{RunTheBake}");

            var missing = new List<string>();
            foreach (string piece in DialogueBubbleKit.Pieces)
                if (AssetDatabase.LoadAssetAtPath<Sprite>(PathFor(piece)) == null)
                    missing.Add(piece);

            Assert.That(missing, Is.Empty,
                        $"{missing.Count} piece(s) do not resolve as sprites.{RunTheBake}");
        }

        [Test]
        public void TheArtFolderHoldsExactlyTheKit_NothingStrayAndNothingMissing()
        {
            Assert.That(Directory.Exists(DialoguePresenter.ArtFolder), Is.True,
                        $"{DialoguePresenter.ArtFolder} does not exist.{RunTheBake}");

            // Enumerated, not sampled: a stray PNG in this folder is either a piece somebody forgot
            // to add to DialogueBubbleKit.Pieces (so nothing imports it) or a leftover from a
            // renamed piece (so it ships dead weight). Both are worth a red test.
            var onDisk = new List<string>();
            foreach (string file in Directory.GetFiles(DialoguePresenter.ArtFolder, "*.png"))
                onDisk.Add(Path.GetFileNameWithoutExtension(file));

            Assert.That(onDisk, Is.EquivalentTo(DialogueBubbleKit.Pieces),
                        "the baked folder and DialogueBubbleKit.Pieces disagree about what the kit " +
                        $"is.{RunTheBake}");
        }

        [Test]
        public void EveryPieceCarriesTheKitsOwnSizeAndBorder()
        {
            Assert.That(Directory.Exists(DialoguePresenter.ArtFolder), Is.True,
                        $"{DialoguePresenter.ArtFolder} does not exist.{RunTheBake}");

            foreach (string piece in DialogueBubbleKit.Pieces)
            {
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PathFor(piece));
                Assert.That(sprite, Is.Not.Null, $"'{piece}' does not resolve.{RunTheBake}");

                Assert.That(sprite.pixelsPerUnit, Is.EqualTo((float)DialogueBubbleKit.PixelsPerUnit).Within(0.01f),
                            $"'{piece}' imported at the wrong PPU — the kit is authored at 32 px/m, " +
                            "the assets grid, NOT the camera-side 24.");

                Vector4 border = ExpectedBorder(piece);
                Assert.That(sprite.border, Is.EqualTo(border),
                            $"'{piece}' has border {sprite.border}, expected {border}. Unity's order " +
                            "is (left, bottom, right, top) — getting it wrong stretches the wrong " +
                            "edges and is silent.");
            }
        }

        [Test]
        public void ThePanelAndTheGoldTileAreTheOnlyNineSlices()
        {
            // The shape of the kit, asserted rather than assumed: exactly two pieces stretch, and
            // every other piece is a fixed stamp whose border must stay zero (a border on a 7×7 tail
            // would let Unity stretch the tip pixel, which is the anchor).
            var sliced = new List<string>();
            foreach (string piece in DialogueBubbleKit.Pieces)
                if (ExpectedBorder(piece) != Vector4.zero) sliced.Add(piece);

            Assert.That(sliced, Is.EquivalentTo(new[]
            {
                DialogueBubbleKit.PiecePanel, DialogueBubbleKit.PieceGold,
            }));
        }

        [Test]
        public void TheArtFolderIsDeclaredOnThePresenter_SoThereIsOnePlaceToLook()
        {
            // Kept from the armed form: the path is not a guess living in a test, it is a constant
            // on the component the kit is wired into. If somebody moves it, they move it once.
            Assert.That(DialoguePresenter.ArtFolder, Does.StartWith("Assets/"),
                        "the kit lands inside the project, not in docs/");
            Assert.That(DialoguePresenter.ArtFolder, Does.Contain("Art"),
                        "and under Art, where every other kit lives");
        }

        /// <summary>The border a piece must carry, from the kit's own declared corners. Mirrors
        /// <c>BubbleKitImporter.BorderFor</c>, which this assembly cannot reference — and
        /// <c>BubbleKitImporterBorderTests</c> asserts the two agree, so the duplication cannot
        /// drift.</summary>
        internal static Vector4 ExpectedBorder(string piece)
        {
            if (piece == DialogueBubbleKit.PiecePanel)
            {
                int c = DialogueBubbleKit.PanelCorner;
                return new Vector4(c, c, c, c);
            }
            if (piece == DialogueBubbleKit.PieceGold)
            {
                int c = DialogueBubbleKit.GoldCorner;
                return new Vector4(c, c, c, c);
            }
            return Vector4.zero;
        }
    }
}
