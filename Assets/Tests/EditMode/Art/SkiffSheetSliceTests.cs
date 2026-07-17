using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Guards the baked slice of the skiff-fleet sheets (two 7 m centre-console hulls, their 8-frame
    /// wave-rock loops, and the shared two-layer remote-steer outboard in both paint builds). The slice
    /// lives in the <c>.meta</c>, not in code, so nothing at runtime would notice it rotting — a
    /// re-export that drifts the grid, a re-slice that loses the custom pivot, or an importer setting
    /// that downscales the sheet all land as silently wrong sprites. These assert the invariants the
    /// art director's kit README fixes, in the style of the other per-lane content tests.
    ///
    /// <para>The load-bearing one is <b>the pivot</b>: the README anchors the hull at (122,120) from a
    /// 244×216 cell's TOP-LEFT and the motor at (136,120) from a 272×216 cell's TOP-LEFT — the motor cell
    /// is deliberately wider so hard-over/raised poses never clip. Flipped to Unity's bottom-left origin
    /// (y = 216−120 = 96), BOTH normalize to (0.5, 0.4444…). That identity is the whole mechanism that
    /// lands the wider motor cell on the transom, so it is asserted per-sprite and across cell sizes.</para>
    /// </summary>
    public class SkiffSheetSliceTests
    {
        private const string Boats = "Assets/_Project/Art/Boats/";

        // The kit README, as expectations: (file, cols, rows, cellW, cellH).
        //   Hulls  — 8 cols × 1 row → index = heading (0 N, 1 NE, 2 E, 3 SE, 4 S, 5 SW, 6 W, 7 NW).
        //   Rock   — 8 cols (wave frame) × 8 rows (heading) → index = heading×8 + frame.
        //   Motor  — 9 cols (steer, −30°..+30°) × 8 rows (heading) → index = heading×9 + steerCol.
        private static readonly (string File, int Cols, int Rows, int CellW, int CellH)[] Sheets =
        {
            ("ConsoleIso.png",            8, 1, 244, 216),
            ("SportSkiffIso.png",         8, 1, 244, 216),
            ("ConsoleIsoRock.png",        8, 8, 244, 216),
            ("SportSkiffIsoRock.png",     8, 8, 244, 216),
            ("SkiffMotorUpper-Work.png",  9, 8, 272, 216),
            ("SkiffMotorLower-Work.png",  9, 8, 272, 216),
            ("SkiffMotorUpper-Sport.png", 9, 8, 272, 216),
            ("SkiffMotorLower-Sport.png", 9, 8, 272, 216),
        };

        // The one boat origin (amidships, keel bottom, centreline) every layer pins to, normalized.
        private const float OriginX = 0.5f;          // 122/244 == 136/272
        private const float OriginY = 96f / 216f;    // (216−120) flipped to bottom-origin

        private static IEnumerable<(string File, int Cols, int Rows, int CellW, int CellH)> AllSheets() => Sheets;

        /// <summary>Multiple-mode sheets return null from LoadAssetAtPath&lt;Sprite&gt; — LoadAllAssets is the rule.</summary>
        private static Sprite[] LoadSlices(string file) =>
            AssetDatabase.LoadAllAssetsAtPath(Boats + file).OfType<Sprite>().ToArray();

        [Test]
        [TestCaseSource(nameof(AllSheets))]
        public void Sheet_IsSlicedMultipleMode_WithTheExpectedCellCount(
            (string File, int Cols, int Rows, int CellW, int CellH) s)
        {
            string path = Boats + s.File;
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.IsNotNull(importer, $"{s.File}: no TextureImporter — is the .meta committed?");
            Assert.AreEqual(SpriteImportMode.Multiple, importer.spriteImportMode,
                            $"{s.File}: must stay grid-sliced (Multiple), not a Single sprite");

            var slices = LoadSlices(s.File);
            Assert.AreEqual(s.Cols * s.Rows, slices.Length,
                            $"{s.File}: expected {s.Cols}×{s.Rows} cells");
        }

        [Test]
        [TestCaseSource(nameof(AllSheets))]
        public void Sheet_ImportsAtNativeRes_NotDownscaled(
            (string File, int Cols, int Rows, int CellW, int CellH) s)
        {
            // Unity's default maxTextureSize is 2048; the 2448-wide motor sheets exceed it. A downscaled
            // sheet cannot carry a source-pixel grid — the rects get refit and alpha-trimmed and the
            // pivot is thrown away. This is the regression guard for that (the slicer lifts the cap).
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Boats + s.File);
            Assert.IsNotNull(tex, $"{s.File}: failed to load as Texture2D");
            Assert.AreEqual(s.Cols * s.CellW, tex.width,
                            $"{s.File}: width is not native — the importer downscaled the sheet");
            Assert.AreEqual(s.Rows * s.CellH, tex.height,
                            $"{s.File}: height is not native — the importer downscaled the sheet");
        }

        [Test]
        [TestCaseSource(nameof(AllSheets))]
        public void EverySlice_SharesTheBoatOriginPivot(
            (string File, int Cols, int Rows, int CellW, int CellH) s)
        {
            // Pixels, not normalized: a heading- or frame-swap must never shift the boat, and the hull
            // and motor layers must never shear apart under the wave.
            float expX = OriginX * s.CellW;
            float expY = OriginY * s.CellH;
            foreach (var sprite in LoadSlices(s.File))
            {
                Assert.AreEqual(expX, sprite.pivot.x, 0.01f, $"{sprite.name}: pivot.x off the boat origin");
                Assert.AreEqual(expY, sprite.pivot.y, 0.01f, $"{sprite.name}: pivot.y off the boat origin");
                Assert.AreEqual(s.CellW, sprite.rect.width, 0.01f, $"{sprite.name}: cell width drifted");
                Assert.AreEqual(s.CellH, sprite.rect.height, 0.01f, $"{sprite.name}: cell height drifted");
            }
        }

        [Test]
        [TestCaseSource(nameof(AllSheets))]
        public void Slices_AreNamed_StemUnderscoreIndex_ContiguousFromZero(
            (string File, int Cols, int Rows, int CellW, int CellH) s)
        {
            // The `_N` suffix IS the index math contract (heading = index/Cols, col = index%Cols) and is
            // what PersistentCoreBuilder.SpriteIndex parses. A gap or a rename silently mis-maps headings.
            string stem = System.IO.Path.GetFileNameWithoutExtension(s.File);
            var indices = new HashSet<int>();
            foreach (var sprite in LoadSlices(s.File))
            {
                StringAssert.StartsWith(stem + "_", sprite.name, $"{sprite.name}: unexpected slice name");
                Assert.IsTrue(int.TryParse(sprite.name.Substring(stem.Length + 1), out int i),
                              $"{sprite.name}: slice name must end in _<index>");
                Assert.IsTrue(indices.Add(i), $"{sprite.name}: duplicate index {i}");
            }
            for (int i = 0; i < s.Cols * s.Rows; i++)
                Assert.IsTrue(indices.Contains(i), $"{stem}: missing slice index {i}");
        }

        [Test]
        public void HullAndMotor_PinToTheSameWorldOrigin_DespiteDifferentCellWidths()
        {
            // The kit's central trick, stated as a test: the motor cell is 28 px wider than the hull cell,
            // yet both README anchors normalize to the SAME pivot — which is exactly why compositing by
            // pivot (never by corner) lands the outboard on the transom.
            Assert.AreEqual(122f / 244f, 136f / 272f, 1e-6f,
                            "hull and motor anchors must normalize identically");

            var hull = LoadSlices("ConsoleIso.png").First();
            var motor = LoadSlices("SkiffMotorUpper-Work.png").First();

            Assert.AreEqual(hull.pivot.x / hull.rect.width, motor.pivot.x / motor.rect.width, 1e-4f,
                            "hull/motor normalized pivot.x diverged — the outboard will sit off-centre");
            Assert.AreEqual(hull.pivot.y / hull.rect.height, motor.pivot.y / motor.rect.height, 1e-4f,
                            "hull/motor normalized pivot.y diverged — the outboard will float off the transom");
        }
    }
}
