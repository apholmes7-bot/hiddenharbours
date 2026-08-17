using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// <b>THE TRIPWIRE for the notebook kit's baked art.</b>
    ///
    /// <para><b>⚠️ THIS IS RED UNTIL THE BAKE RUNS, AND THAT IS DELIBERATE.</b> The cloud lane cannot
    /// run Unity, so this PR lands the baker and the importer but no PNGs. Gating these assertions on
    /// the sprites existing — <c>if (!File.Exists) Assert.Pass()</c> — would be exactly the vacuous
    /// green the cloud-lane protocol forbids: a test that passes while the art is absent would have
    /// passed for the entire time it was absent, and would go on passing if the bake silently stopped
    /// producing a piece. So EXISTENCE IS THE FIRST ASSERTION, and the failure message names the menu
    /// item that fixes it.</para>
    ///
    /// <para>Expect red on the first CI run and green after the coordinator's bake:
    /// <c>Hidden Harbours ▸ Art ▸ Bake Notebook Kit</c>, or headless
    /// <c>-executeMethod HiddenHarbours.Tools.RigBaking.NotebookKitBakeMenu.BakeAndImportHeadless</c>.</para>
    ///
    /// <para>The reverse arm is kept too: once the art is in, this fails the day it LEAVES, or the day
    /// a stray piece appears in the folder that nothing bakes.</para>
    /// </summary>
    public class NotebookArtTests
    {
        static string PathFor(string piece) => $"{NotebookKit.ArtFolder}/{piece}.png";

        const string HowToFix =
            "Run: Hidden Harbours ▸ Art ▸ Bake Notebook Kit — or headless, -executeMethod " +
            "HiddenHarbours.Tools.RigBaking.NotebookKitBakeMenu.BakeAndImportHeadless (twice on a " +
            "fresh worktree: a first Unity run can import only and never call the method).";

        [Test]
        public void EveryBakedPieceIsOnDisk()
        {
            var missing = NotebookKit.Pieces.Where(p => !File.Exists(PathFor(p))).ToList();
            Assert.That(missing, Is.Empty,
                $"{missing.Count} of {NotebookKit.Pieces.Length} notebook pieces are not baked.\n" +
                HowToFix + "\nMissing:\n  " + string.Join("\n  ", missing));
        }

        [Test]
        public void EveryPieceResolvesAsASpriteAtTheKitsPixelsPerUnit()
        {
            foreach (string piece in NotebookKit.Pieces)
            {
                string path = PathFor(piece);
                Assert.That(File.Exists(path), Is.True, $"{piece} is not baked. {HowToFix}");

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                Assert.That(sprite, Is.Not.Null,
                    $"{piece} is on disk but does not resolve as a Sprite — the importer has not run.");
                Assert.That(sprite.pixelsPerUnit, Is.EqualTo((float)NotebookKit.PixelsPerUnit).Within(0.01f),
                    $"{piece} imported at PPU {sprite.pixelsPerUnit} — is the folder still under " +
                    "Assets/_Project/Art/, where ArtImportPipeline's lock reaches it?");
            }
        }

        [Test]
        public void TheFolderHoldsExactlyWhatTheKitBakes()
        {
            // The reverse arm. A stray PNG here is either a piece someone baked and forgot to
            // register, or one that was renamed and left its old self behind.
            Assert.That(Directory.Exists(NotebookKit.ArtFolder), Is.True,
                $"{NotebookKit.ArtFolder} does not exist. {HowToFix}");

            var onDisk = Directory.GetFiles(NotebookKit.ArtFolder, "*.png")
                .Select(Path.GetFileNameWithoutExtension)
                .OrderBy(n => n, System.StringComparer.Ordinal)
                .ToArray();

            var expected = NotebookKit.Pieces
                .OrderBy(n => n, System.StringComparer.Ordinal)
                .ToArray();

            Assert.That(onDisk, Is.EqualTo(expected),
                "The baked folder and NotebookKit.Pieces disagree. Extra files are strays; missing " +
                $"files need the bake.\n{HowToFix}");
        }

        [Test]
        public void NoPieceIsAnAccidentalDuplicateOfAnother()
        {
            // cursor.cuff was caught this way before it could ship: it rendered byte-for-byte
            // identical to cursor.hand because the kit names a cursor the bubble kit does not have.
            // Any future duplicate is the same mistake wearing a different name.
            var missing = NotebookKit.Pieces.Where(p => !File.Exists(PathFor(p))).ToList();
            Assert.That(missing, Is.Empty, $"cannot compare pieces that are not baked. {HowToFix}");

            using var sha = System.Security.Cryptography.SHA256.Create();
            var byHash = NotebookKit.Pieces
                .Select(p => (Piece: p, Hash: System.Convert.ToBase64String(
                    sha.ComputeHash(File.ReadAllBytes(PathFor(p))))))
                .GroupBy(t => t.Hash)
                .Where(g => g.Count() > 1)
                .Select(g => string.Join(" == ", g.Select(t => t.Piece)))
                .ToList();

            Assert.That(byHash, Is.Empty,
                "These pieces baked to identical bytes, so one of them is not the alternate its name " +
                "claims:\n  " + string.Join("\n  ", byHash));
        }
    }
}
