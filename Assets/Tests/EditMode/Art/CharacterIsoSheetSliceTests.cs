using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Guards the baked slice of the 8-direction ISO CHARACTER sheets (Fisher, Ginny, Skipper —
    /// idle/walk/run). The slice lives in the <c>.meta</c>, not in code, so nothing at runtime would
    /// notice it rotting: a re-export that drifts the grid, a re-slice that loses the ground pivot, or
    /// an importer setting that downscales the sheet all land as silently wrong sprites.
    ///
    /// <para><b>Expectations come from the ART, not from the slicer.</b> Every count here is derived
    /// from the actual PNG dimensions read off disk (<c>cols = width / 64</c>, <c>rows = height / 88</c>)
    /// — asserting the slicer's grid config against the slicer's grid config is the self-referential
    /// blind spot that let the mirrored boat art ship, and it is deliberately avoided.</para>
    ///
    /// <para>The load-bearing assert is <b>the pivot</b>, in pixels. The README anchors ground contact at
    /// (32, 80) from the cell's TOP-LEFT; Unity normalizes from the BOTTOM-LEFT, so the pivot must be
    /// (32, 8) in pixels. Inverting it plants every character ~72 px ≈ 2.25 m at PPU 32 into the ground.
    /// It is also the only assert that catches Unity's texture-size-cap downscale, where the sprite
    /// COUNT still matches but every rect has been refit.</para>
    ///
    /// <para>Row order is asserted only as a <i>count</i> of 8. These rows are baked COUNTER-CLOCKWISE
    /// while the README labels them clockwise (true order N · NW · W · SW · S · SE · E · NE — row i
    /// depicts heading −45°·i), so the slices are named by row INDEX and no compass mapping is encoded
    /// anywhere in this PR. See <c>CharacterSheetSlicer</c>'s remarks.</para>
    /// </summary>
    public class CharacterIsoSheetSliceTests
    {
        private const string Iso = "Assets/_Project/Art/Characters/Iso/";

        // The README's cell, stated once here as the contract under test. Everything else (frame count,
        // row count, total sprites) is DERIVED from the PNG on disk.
        private const int CellW = 64;
        private const int CellH = 88;

        // Ground contact: README (32,80) from the TOP-LEFT of an 88-tall cell → (32, 8) bottom-origin.
        private const float PivotPxX = 32f;
        private const float PivotPxY = CellH - 80f; // == 8

        private static readonly string[] Stems =
        {
            "Fisher_idle",  "Fisher_walk",  "Fisher_run",
            "Ginny_idle",   "Ginny_walk",   "Ginny_run",
            "Skipper_idle", "Skipper_walk", "Skipper_run",
        };

        private static IEnumerable<string> AllSheets() => Stems;

        /// <summary>⚠️ Multiple-mode sheets return null from LoadAssetAtPath&lt;Sprite&gt; — LoadAllAssets is the rule.</summary>
        private static Sprite[] LoadSlices(string stem) =>
            AssetDatabase.LoadAllAssetsAtPath(Iso + stem + ".png").OfType<Sprite>().ToArray();

        private static Texture2D LoadSheet(string stem)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Iso + stem + ".png");
            Assert.IsNotNull(tex, $"{stem}.png: failed to load as Texture2D — is the PNG (and its .meta) committed?");
            return tex;
        }

        [Test]
        [TestCaseSource(nameof(AllSheets))]
        public void Sheet_IsSlicedMultipleMode_IntoEightDirectionRowsOfTheArtsOwnFrameCount(string stem)
        {
            var importer = AssetImporter.GetAtPath(Iso + stem + ".png") as TextureImporter;
            Assert.IsNotNull(importer, $"{stem}: no TextureImporter — is the .meta committed?");
            Assert.AreEqual(SpriteImportMode.Multiple, importer.spriteImportMode,
                            $"{stem}: must stay grid-sliced (Multiple), not a Single sprite");

            var tex = LoadSheet(stem);

            // Derived from the art, not asserted against a constant.
            Assert.AreEqual(0, tex.width % CellW,
                            $"{stem}: {tex.width} px wide is not a whole number of {CellW} px cells");
            Assert.AreEqual(0, tex.height % CellH,
                            $"{stem}: {tex.height} px tall is not a whole number of {CellH} px cells");

            int cols = tex.width / CellW;
            int rows = tex.height / CellH;

            Assert.AreEqual(8, rows, $"{stem}: an iso character sheet must have 8 direction rows");
            Assert.AreEqual(rows * cols, LoadSlices(stem).Length,
                            $"{stem}: expected {rows} direction rows × {cols} frames = {rows * cols} slices");
        }

        [Test]
        [TestCaseSource(nameof(AllSheets))]
        public void Sheet_ImportsAtNativeRes_NotDownscaled(string stem)
        {
            // These sheets are 704 px tall — comfortably under the 2048 default cap — so this should not
            // bite. Assert it anyway: a downscaled sheet cannot carry a source-pixel grid (rects get refit
            // and the pivot is thrown away) while the sprite COUNT still matches, so only this and the
            // pivot test would ever catch it.
            var tex = LoadSheet(stem);
            var slices = LoadSlices(stem);
            Assert.IsNotEmpty(slices, $"{stem}: no slices loaded");

            float maxRight = slices.Max(s => s.rect.xMax);
            float maxTop = slices.Max(s => s.rect.yMax);
            Assert.AreEqual(tex.width, maxRight, 0.01f,
                            $"{stem}: slices do not span the sheet width — importer downscaled or grid drifted");
            Assert.AreEqual(tex.height, maxTop, 0.01f,
                            $"{stem}: slices do not span the sheet height — importer downscaled or grid drifted");
        }

        [Test]
        [TestCaseSource(nameof(AllSheets))]
        public void EverySlice_IsOneCell_AndPivotsOnGroundContact(string stem)
        {
            // ⚠️ Pixels, not normalized. A flipped pivot (0.5, 80/88 instead of 0.5, 8/88) reads as a
            // plausible number but buries the character ~72 px in the ground on every frame.
            var slices = LoadSlices(stem);
            Assert.IsNotEmpty(slices, $"{stem}: no slices loaded");
            foreach (var s in slices)
            {
                Assert.AreEqual(CellW, s.rect.width, 0.01f, $"{s.name}: cell width drifted");
                Assert.AreEqual(CellH, s.rect.height, 0.01f, $"{s.name}: cell height drifted");
                Assert.AreEqual(PivotPxX, s.pivot.x, 0.01f, $"{s.name}: pivot.x off the character centreline");
                Assert.AreEqual(PivotPxY, s.pivot.y, 0.01f,
                                $"{s.name}: pivot.y off ground contact — is it inverted? " +
                                $"README (32,80) is TOP-LEFT; Unity wants bottom-origin {PivotPxY}");
            }
        }

        [Test]
        [TestCaseSource(nameof(AllSheets))]
        public void Slices_TileTheSheet_WithNoGapsAndNoOverlap(string stem)
        {
            // Every (col,row) origin the sheet's own dimensions imply must be covered exactly once.
            var tex = LoadSheet(stem);
            int cols = tex.width / CellW;
            int rows = tex.height / CellH;

            var occupied = new HashSet<(int, int)>();
            foreach (var s in LoadSlices(stem))
            {
                Assert.AreEqual(0, Mathf.RoundToInt(s.rect.x) % CellW, $"{s.name}: x not on the cell grid");
                Assert.AreEqual(0, Mathf.RoundToInt(s.rect.y) % CellH, $"{s.name}: y not on the cell grid");
                var cell = (Mathf.RoundToInt(s.rect.x) / CellW, Mathf.RoundToInt(s.rect.y) / CellH);
                Assert.IsTrue(occupied.Add(cell), $"{s.name}: two slices overlap cell {cell}");
            }

            for (int c = 0; c < cols; c++)
                for (int r = 0; r < rows; r++)
                    Assert.IsTrue(occupied.Contains((c, r)), $"{stem}: no slice covers cell (col {c}, row {r})");
        }

        [Test]
        [TestCaseSource(nameof(AllSheets))]
        public void Slices_AreNamedByRowIndex_NotByCompassName(string stem)
        {
            // The `_d<row>_f<col>` scheme IS the contract, and the ABSENCE of a compass name is
            // deliberate: these rows are baked counter-clockwise while the README labels them clockwise,
            // so "…_E" or "…_NE" in a sprite name would hard-code the mislabelling into the asset
            // database. A forthcoming CharacterVisualDef carries the FacingsAreCounterClockwise flag.
            var tex = LoadSheet(stem);
            int cols = tex.width / CellW;
            int rows = tex.height / CellH;

            var seen = new HashSet<string>();
            foreach (var s in LoadSlices(stem))
            {
                StringAssert.StartsWith(stem + "_d", s.name, $"{s.name}: unexpected slice name");
                Assert.IsTrue(seen.Add(s.name), $"{s.name}: duplicate slice name");

                // Row index must map to the rect's own row — the name and the geometry must agree.
                string tail = s.name.Substring(stem.Length + 2);          // "<row>_f<col>"
                string[] parts = tail.Split(new[] { "_f" }, System.StringSplitOptions.None);
                Assert.AreEqual(2, parts.Length, $"{s.name}: must be <stem>_d<row>_f<col>");
                Assert.IsTrue(int.TryParse(parts[0], out int d), $"{s.name}: unparseable row index");
                Assert.IsTrue(int.TryParse(parts[1], out int f), $"{s.name}: unparseable frame index");
                Assert.Less(d, rows, $"{s.name}: row index out of range");
                Assert.Less(f, cols, $"{s.name}: frame index out of range");

                // Row 0 is the TOP row of the canvas; Unity rects are bottom-origin.
                int rectRowFromTop = rows - 1 - Mathf.RoundToInt(s.rect.y) / CellH;
                Assert.AreEqual(d, rectRowFromTop,
                                $"{s.name}: name says row {d} but the rect sits at row {rectRowFromTop} from the top");
                Assert.AreEqual(f, Mathf.RoundToInt(s.rect.x) / CellW,
                                $"{s.name}: name says frame {f} but the rect sits in a different column");
            }

            foreach (var s in LoadSlices(stem))
                StringAssert.DoesNotContain("_N", s.name.Substring(stem.Length),
                                            $"{s.name}: compass names must not be baked into slice names");
        }

        [Test]
        public void FrameCounts_MatchTheArtDirectorsReadme_IdleSixWalkEightRunSix()
        {
            // The one place the README's stated frame counts are checked — against the PNGs, so a
            // re-export that quietly changed an animation's length is caught rather than absorbed.
            foreach (var stem in Stems)
            {
                var tex = LoadSheet(stem);
                int cols = tex.width / CellW;
                int expected = stem.EndsWith("_walk") ? 8 : 6;
                Assert.AreEqual(expected, cols,
                                $"{stem}: README says {expected} frames but the sheet is {tex.width} px " +
                                $"wide = {cols} cells");
            }
        }

        [Test]
        public void EveryIsoCharacterPngInTheFolder_IsCoveredByThisTest()
        {
            // A tenth sheet dropped into the folder must not slip past the guard unnoticed.
            var onDisk = Directory.GetFiles(Iso, "*.png")
                                  .Select(Path.GetFileNameWithoutExtension)
                                  .OrderBy(s => s)
                                  .ToArray();
            CollectionAssert.AreEquivalent(Stems.OrderBy(s => s).ToArray(), onDisk,
                                           "Iso character sheets on disk differ from the guarded set");
        }
    }
}
