using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Guards the baked slice of the 8-direction ISO CHARACTER sheets — the locomotion set (Fisher,
    /// Ginny, Skipper — idle/walk/run) and the Fisher's rod poses (hold, cast_short, cast_long). The
    /// slice lives in the <c>.meta</c>, not in code, so nothing at runtime would notice it rotting: a
    /// re-export that drifts the grid, a re-slice that loses the ground pivot, or an importer setting
    /// that downscales the sheet all land as silently wrong sprites.
    ///
    /// <para><b>⚠️ TWO CELL SIZES.</b> The locomotion sheets are <b>64 × 88</b>. The rod sheets are
    /// <b>128 × 128</b> — the same figure on a canvas padded +32 px each side and +40 px on top for the
    /// rod arc and the flying lure. The ground line does not move, so the pivot rule is the same on
    /// both: <b>8 px above the cell bottom, on the centreline</b> → (0.5, 8/88 ≈ 0.0909) for locomotion
    /// and (0.5, 8/128 = 0.0625) for the rod sheets. Inverting it buries the character ~72 px
    /// (locomotion) or ~112 px (rod) in the ground. The pivot assert below is stated in PIXELS so both
    /// cell sizes are checked by the same rule, and it is deliberately duplicated as a literal here
    /// rather than imported from the slicer.</para>
    ///
    /// <para><b>Expectations come from the ART, not from the slicer.</b> Frame counts, row counts and
    /// total sprite counts are all derived from the actual PNG dimensions read off disk
    /// (<c>cols = width / cellW</c>, <c>rows = height / cellH</c>) — asserting the slicer's grid config
    /// against the slicer's grid config is the self-referential blind spot that let the mirrored boat
    /// art ship, and it is deliberately avoided. This test never references
    /// <c>CharacterSheetSlicer</c>. The one thing that cannot be derived is the cell size itself (1280
    /// px is a whole number of both 64 px and 128 px cells), so it is restated here independently as
    /// the contract under test.</para>
    ///
    /// <para>Row order is asserted only as a <i>count</i> of 8. These rows are baked COUNTER-CLOCKWISE
    /// while the README labels them clockwise (true order N · NW · W · SW · S · SE · E · NE — row i
    /// depicts heading −45°·i), so the slices are named by row INDEX and no compass mapping is encoded
    /// anywhere in this PR. See <c>CharacterSheetSlicer</c>'s remarks — including the note that the rod
    /// poses carry a rig <c>yaw:16</c>, which skews face-centroid checks on those three sheets.</para>
    /// </summary>
    public class CharacterIsoSheetSliceTests
    {
        private const string Iso = "Assets/_Project/Art/Characters/Iso/";

        /// <summary>Ground contact sits this many px above the cell bottom on EVERY sheet.</summary>
        private const float GroundInsetPx = 8f;

        /// <summary>
        /// The authored cell of each sheet, stated here as the contract under test. Everything else
        /// (frame count, row count, total sprites) is DERIVED from the PNG on disk.
        /// </summary>
        private static readonly Dictionary<string, Vector2Int> Sheets = new Dictionary<string, Vector2Int>
        {
            // Locomotion — 64 × 88.
            { "Fisher_idle",  new Vector2Int(64, 88) },
            { "Fisher_walk",  new Vector2Int(64, 88) },
            { "Fisher_run",   new Vector2Int(64, 88) },
            { "Ginny_idle",   new Vector2Int(64, 88) },
            { "Ginny_walk",   new Vector2Int(64, 88) },
            { "Ginny_run",    new Vector2Int(64, 88) },
            { "Skipper_idle", new Vector2Int(64, 88) },
            { "Skipper_walk", new Vector2Int(64, 88) },
            { "Skipper_run",  new Vector2Int(64, 88) },

            // Rod poses — 128 × 128.
            { "Fisher_hold",       new Vector2Int(128, 128) },
            { "Fisher_cast_short", new Vector2Int(128, 128) },
            { "Fisher_cast_long",  new Vector2Int(128, 128) },
        };

        /// <summary>The README's / drop's stated frame counts, checked against the PNG widths.</summary>
        private static readonly Dictionary<string, int> ExpectedFrames = new Dictionary<string, int>
        {
            { "Fisher_idle", 6 },  { "Fisher_walk", 8 },  { "Fisher_run", 6 },
            { "Ginny_idle", 6 },   { "Ginny_walk", 8 },   { "Ginny_run", 6 },
            { "Skipper_idle", 6 }, { "Skipper_walk", 8 }, { "Skipper_run", 6 },
            { "Fisher_hold", 6 },  { "Fisher_cast_short", 10 }, { "Fisher_cast_long", 10 },
        };

        private static IEnumerable<string> AllSheets() => Sheets.Keys.OrderBy(s => s).ToArray();

        private static Vector2Int Cell(string stem) => Sheets[stem];

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
            Vector2Int cell = Cell(stem);

            // Derived from the art, not asserted against a constant.
            Assert.AreEqual(0, tex.width % cell.x,
                            $"{stem}: {tex.width} px wide is not a whole number of {cell.x} px cells");
            Assert.AreEqual(0, tex.height % cell.y,
                            $"{stem}: {tex.height} px tall is not a whole number of {cell.y} px cells");

            int cols = tex.width / cell.x;
            int rows = tex.height / cell.y;

            Assert.AreEqual(8, rows, $"{stem}: an iso character sheet must have 8 direction rows");
            Assert.AreEqual(rows * cols, LoadSlices(stem).Length,
                            $"{stem}: expected {rows} direction rows × {cols} frames = {rows * cols} slices");
        }

        [Test]
        [TestCaseSource(nameof(AllSheets))]
        public void Sheet_ImportsAtNativeRes_NotDownscaled(string stem)
        {
            // The widest sheet here is 1280 px — comfortably under the 2048 default cap — so this should
            // not bite. Assert it anyway: a downscaled sheet cannot carry a source-pixel grid (rects get
            // refit and the pivot is thrown away) while the sprite COUNT still matches, so only this and
            // the pivot test would ever catch it.
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
            // ⚠️ Pixels, not normalized — one rule for both cell sizes: centreline, 8 px above the cell
            // bottom. A flipped pivot (8/88 → 80/88, or 8/128 → 120/128) reads as a plausible number but
            // buries the character 72 px (64×88) / 112 px (128×128) in the ground on every frame.
            Vector2Int cell = Cell(stem);
            float pivotPxX = cell.x / 2f;
            float pivotPxY = GroundInsetPx;

            var slices = LoadSlices(stem);
            Assert.IsNotEmpty(slices, $"{stem}: no slices loaded");
            foreach (var s in slices)
            {
                Assert.AreEqual(cell.x, s.rect.width, 0.01f, $"{s.name}: cell width drifted");
                Assert.AreEqual(cell.y, s.rect.height, 0.01f, $"{s.name}: cell height drifted");
                Assert.AreEqual(pivotPxX, s.pivot.x, 0.01f, $"{s.name}: pivot.x off the character centreline");
                Assert.AreEqual(pivotPxY, s.pivot.y, 0.01f,
                                $"{s.name}: pivot.y off ground contact — is it inverted? ground contact is " +
                                $"({pivotPxX}, {cell.y - GroundInsetPx}) TOP-LEFT; Unity wants bottom-origin {pivotPxY}");
            }
        }

        [Test]
        [TestCaseSource(nameof(AllSheets))]
        public void EverySlice_NormalizedPivot_IsGroundInsetOverCellHeight(string stem)
        {
            // The same rule again in NORMALIZED terms, because that is the number actually stored in the
            // .meta and the number a future presenter will reason about: (0.5, 8/cellH). For the rod
            // sheets that is 0.0625; for the locomotion sheets ≈ 0.0909. Two different numbers, one rule
            // — and this catches a "generalisation" that quietly reused 8/88 on a 128-tall cell.
            Vector2Int cell = Cell(stem);
            float expectedY = GroundInsetPx / cell.y;

            foreach (var s in LoadSlices(stem))
            {
                Vector2 norm = new Vector2(s.pivot.x / s.rect.width, s.pivot.y / s.rect.height);
                Assert.AreEqual(0.5f, norm.x, 0.0005f, $"{s.name}: normalized pivot.x must be 0.5");
                Assert.AreEqual(expectedY, norm.y, 0.0005f,
                                $"{s.name}: normalized pivot.y must be {GroundInsetPx}/{cell.y} = {expectedY}");
            }
        }

        [Test]
        [TestCaseSource(nameof(AllSheets))]
        public void Slices_TileTheSheet_WithNoGapsAndNoOverlap(string stem)
        {
            // Every (col,row) origin the sheet's own dimensions imply must be covered exactly once.
            var tex = LoadSheet(stem);
            Vector2Int cell = Cell(stem);
            int cols = tex.width / cell.x;
            int rows = tex.height / cell.y;

            var occupied = new HashSet<(int, int)>();
            foreach (var s in LoadSlices(stem))
            {
                Assert.AreEqual(0, Mathf.RoundToInt(s.rect.x) % cell.x, $"{s.name}: x not on the cell grid");
                Assert.AreEqual(0, Mathf.RoundToInt(s.rect.y) % cell.y, $"{s.name}: y not on the cell grid");
                var c = (Mathf.RoundToInt(s.rect.x) / cell.x, Mathf.RoundToInt(s.rect.y) / cell.y);
                Assert.IsTrue(occupied.Add(c), $"{s.name}: two slices overlap cell {c}");
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
            Vector2Int cell = Cell(stem);
            int cols = tex.width / cell.x;
            int rows = tex.height / cell.y;

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
                int rectRowFromTop = rows - 1 - Mathf.RoundToInt(s.rect.y) / cell.y;
                Assert.AreEqual(d, rectRowFromTop,
                                $"{s.name}: name says row {d} but the rect sits at row {rectRowFromTop} from the top");
                Assert.AreEqual(f, Mathf.RoundToInt(s.rect.x) / cell.x,
                                $"{s.name}: name says frame {f} but the rect sits in a different column");

                StringAssert.DoesNotContain("_N", s.name.Substring(stem.Length),
                                            $"{s.name}: compass names must not be baked into slice names");
            }
        }

        [Test]
        public void FrameCounts_MatchTheDrop_LocomotionSixOrEight_HoldSix_CastTen()
        {
            // The one place the stated frame counts are checked — against the PNGs, so a re-export that
            // quietly changed an animation's length is caught rather than absorbed.
            foreach (var kv in ExpectedFrames)
            {
                var tex = LoadSheet(kv.Key);
                int cols = tex.width / Cell(kv.Key).x;
                Assert.AreEqual(kv.Value, cols,
                                $"{kv.Key}: expected {kv.Value} frames but the sheet is {tex.width} px " +
                                $"wide = {cols} cells of {Cell(kv.Key).x} px");
            }
        }

        [Test]
        public void RodSheets_AreTheBiggerCanvas_NotTheLocomotionCell()
        {
            // The whole point of this PR, asserted against the pixels: the rod sheets are 1024 px tall,
            // which is 8 × 128 and NOT 8 × 88 (= 704). Reading them on the locomotion grid would yield a
            // wrong 20/12 frames and a wrong row height, so pin the dimension itself.
            foreach (var stem in new[] { "Fisher_hold", "Fisher_cast_short", "Fisher_cast_long" })
            {
                var tex = LoadSheet(stem);
                Assert.AreEqual(1024, tex.height,
                                $"{stem}: rod sheets must be 8 rows × 128 px = 1024 px tall");
                Assert.AreNotEqual(0, tex.height % 88,
                                   $"{stem}: 1024 must NOT be a whole number of 88 px rows");
                Assert.AreEqual(0, tex.width % 128,
                                $"{stem}: {tex.width} px wide is not a whole number of 128 px cells");
            }
        }

        [Test]
        public void EveryIsoCharacterPngInTheFolder_IsCoveredByThisTest()
        {
            // A thirteenth sheet dropped into the folder must not slip past the guard unnoticed.
            var onDisk = Directory.GetFiles(Iso, "*.png")
                                  .Select(Path.GetFileNameWithoutExtension)
                                  .OrderBy(s => s)
                                  .ToArray();
            CollectionAssert.AreEquivalent(Sheets.Keys.OrderBy(s => s).ToArray(), onDisk,
                                           "Iso character sheets on disk differ from the guarded set");
        }
    }
}
