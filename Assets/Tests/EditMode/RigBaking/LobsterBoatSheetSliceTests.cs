using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// Guards the lobster boat's sheets — the first artwork in this project baked IN-ENGINE from the
    /// art director's rig rather than hand-exported from a browser (ADR 0021).
    ///
    /// <para><b>THE DOWNSCALE TRAP IS THE REASON THIS FIXTURE EXISTS.</b> Her rock pages are
    /// 3648×3360, both dimensions past Unity's default 2048 <c>maxTextureSize</c>. Without the cap
    /// lift they import at 0.56×, and the failure is SILENT — the sprite COUNT still comes out 64,
    /// exactly as expected, on a sheet that is now mush. Only the explicit cell-rect and pivot
    /// assertions below can tell the difference, which is why the cell size is asserted outright
    /// rather than implied by the count.</para>
    ///
    /// <para><b>She is 32 facings, and her facings are genuinely CLOCKWISE.</b> Every hand-exported
    /// kit in this repo is counter-clockwise and carries
    /// <c>FacingsAreCounterClockwise = true</c> to correct it at runtime. Hers is corrected at BAKE
    /// time instead, because the baker measured the rig's convention from rendered pixels and chose
    /// the <c>dir</c> argument accordingly. Do not "fix" her to match her neighbours.</para>
    /// </summary>
    public class LobsterBoatSheetSliceTests
    {
        const string Boats = "Assets/_Project/Art/Boats/";

        /// <summary>(file, cols, rows, cellW, cellH). 8 cols × N rows, row-major from top-left;
        /// flat index = heading×rockFrames + frame.</summary>
        static readonly (string File, int Cols, int Rows, int CellW, int CellH)[] Sheets =
        {
            ("LobsterBoatIso.png",      8, 4, 456, 420),   // 32 facings, no rock
            ("LobsterBoatIsoRock0.png", 8, 8, 456, 420),   // headings  0–15 × 4 rock frames
            ("LobsterBoatIsoRock1.png", 8, 8, 456, 420),   // headings 16–31 × 4 rock frames
        };

        /// <summary>Read from the rig (pivot 228,258 from the cell's TOP-left), not calibrated by
        /// eye: y = (420 − 258) / 420. Contrast SpriteSheetSlicer.CapeIslanderOrigin, which had to
        /// be recovered from pixels with ≈±4 px of honest uncertainty because her rig was not in the
        /// repo at the time. That is the metadata win ADR 0021 was after.</summary>
        const float OriginX = 228f / 456f;
        const float OriginY = 162f / 420f;

        static IEnumerable<(string File, int Cols, int Rows, int CellW, int CellH)> AllSheets() => Sheets;

        /// <summary>Multiple-mode sheets return null from LoadAssetAtPath&lt;Sprite&gt;.</summary>
        static Sprite[] LoadSlices(string file) =>
            AssetDatabase.LoadAllAssetsAtPath(Boats + file).OfType<Sprite>().ToArray();

        [Test]
        [TestCaseSource(nameof(AllSheets))]
        public void Sheet_IsSlicedMultipleMode_WithTheExpectedCellCount(
            (string File, int Cols, int Rows, int CellW, int CellH) s)
        {
            var importer = AssetImporter.GetAtPath(Boats + s.File) as TextureImporter;
            Assert.IsNotNull(importer, $"{s.File}: no TextureImporter — is the .meta committed?");
            Assert.AreEqual(SpriteImportMode.Multiple, importer.spriteImportMode,
                $"{s.File}: must stay grid-sliced (Multiple).");
            Assert.AreEqual(s.Cols * s.Rows, LoadSlices(s.File).Length,
                $"{s.File}: expected {s.Cols}×{s.Rows} cells.");
        }

        /// <summary>
        /// ⚠️ THE SABOTAGE TARGET. Drop maxTextureSize below the sheet's long edge and this goes red
        /// — which is the whole point, because nothing else would. The sprite count survives a
        /// downscale unharmed.
        /// </summary>
        [Test]
        [TestCaseSource(nameof(AllSheets))]
        public void Sheet_ImportsAtNativeRes_NotSilentlyDownscaled(
            (string File, int Cols, int Rows, int CellW, int CellH) s)
        {
            string path = Boats + s.File;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.IsNotNull(importer, $"{s.File}: no TextureImporter.");

            int longEdge = Mathf.Max(s.Cols * s.CellW, s.Rows * s.CellH);
            Assert.GreaterOrEqual(importer.maxTextureSize, longEdge,
                $"{s.File}: maxTextureSize is {importer.maxTextureSize} but the sheet's long edge " +
                $"is {longEdge}. Unity will import this DOWNSCALED, the sprite count will still " +
                "come out right, and only this assert and the cell rects below will notice.");

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.IsNotNull(tex, $"{s.File}: texture did not load.");
            Assert.AreEqual(s.Cols * s.CellW, tex.width,  $"{s.File}: width is not native.");
            Assert.AreEqual(s.Rows * s.CellH, tex.height, $"{s.File}: height is not native.");
        }

        [Test]
        [TestCaseSource(nameof(AllSheets))]
        public void EverySlice_HasTheExactCellRect_AndTheRigDerivedPivot(
            (string File, int Cols, int Rows, int CellW, int CellH) s)
        {
            var slices = LoadSlices(s.File);
            CollectionAssert.IsNotEmpty(slices, $"{s.File}: no sprites.");

            foreach (var sp in slices)
            {
                Assert.AreEqual(s.CellW, Mathf.RoundToInt(sp.rect.width),
                    $"{s.File}/{sp.name}: cell width is {sp.rect.width}, expected {s.CellW}. " +
                    "A wrong cell width almost always means the sheet imported downscaled.");
                Assert.AreEqual(s.CellH, Mathf.RoundToInt(sp.rect.height),
                    $"{s.File}/{sp.name}: cell height is {sp.rect.height}, expected {s.CellH}.");

                Vector2 pivot = new Vector2(sp.pivot.x / sp.rect.width, sp.pivot.y / sp.rect.height);
                Assert.AreEqual(OriginX, pivot.x, 0.005f, $"{s.File}/{sp.name}: pivot X drifted.");
                Assert.AreEqual(OriginY, pivot.y, 0.005f, $"{s.File}/{sp.name}: pivot Y drifted.");
            }
        }

        /// <summary>
        /// 32 facings is the owner's decision and the reason the baker exists. If a future change
        /// quietly re-bakes her at 8, a big hull goes back to snapping between 45° steps — which is
        /// exactly the thing ADR 0021 set out to fix.
        /// </summary>
        [Test]
        public void SheDoesNotRegressBelow32Facings()
        {
            Assert.AreEqual(32, LoadSlices("LobsterBoatIso.png").Length,
                "The lobster boat's base sheet must carry 32 facings.");
            Assert.AreEqual(128,
                LoadSlices("LobsterBoatIsoRock0.png").Length + LoadSlices("LobsterBoatIsoRock1.png").Length,
                "Her rock grid must carry 32 facings × 4 frames across two pages.");
        }
    }
}
