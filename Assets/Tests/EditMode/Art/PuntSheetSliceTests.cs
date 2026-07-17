using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Guards the baked slice of the iso-punt kit (the ~5.2 m tiller punt, her 8-frame wave-rock loop,
    /// and her two-layer tiller outboard in both paint builds). The slice lives in the <c>.meta</c>, not
    /// in code, so nothing at runtime would notice it rotting — a re-export that drifts the grid, a
    /// re-slice that loses the custom pivot, or an importer setting that downscales the sheet all land as
    /// silently wrong sprites. Mirrors <see cref="SkiffSheetSliceTests"/> for the punt's own kit.
    ///
    /// <para>The load-bearing one is <b>the pivot</b>: the README anchors the hull at (92,94) from a
    /// 184×168 cell's TOP-LEFT and the motor at (106,94) from a 212×168 cell's TOP-LEFT — the motor cell
    /// is deliberately wider so hard-over/raised poses never clip. Flipped to Unity's bottom-left origin
    /// (y = 168−94 = 74), BOTH normalize to (0.5, 0.440476…). That identity is the whole mechanism that
    /// lands the wider motor cell on the transom, so it is asserted per-sprite and across cell sizes.</para>
    ///
    /// <para><b>This is NOT the skiffs' pivot.</b> The punt derives hers from a smaller cell, so her
    /// origin sits at y ≈ 0.4405 where the skiffs' sits at 0.4444. The two must never be conflated — see
    /// <see cref="PuntOrigin_IsDistinctFromTheSkiffOrigin"/>.</para>
    /// </summary>
    public class PuntSheetSliceTests
    {
        private const string Boats = "Assets/_Project/Art/Boats/";

        // The kit README, as expectations: (file, cols, rows, cellW, cellH).
        //   Hull   — 8 cols × 1 row → index = heading (0 N, 1 NE, 2 E, 3 SE, 4 S, 5 SW, 6 W, 7 NW).
        //   Rock   — 8 cols (wave frame) × 8 rows (heading) → index = heading×8 + frame.
        //   Motor  — 9 cols (steer, −32°..+32°, 8° steps) × 8 rows (heading) → index = heading×9 + steerCol.
        private static readonly (string File, int Cols, int Rows, int CellW, int CellH)[] Sheets =
        {
            ("PuntIso.png",                 8, 1, 184, 168),
            ("PuntIsoRock.png",             8, 8, 184, 168),
            ("PuntMotorUpper-Basic.png",    9, 8, 212, 168),
            ("PuntMotorLower-Basic.png",    9, 8, 212, 168),
            ("PuntMotorUpper-Upgraded.png", 9, 8, 212, 168),
            ("PuntMotorLower-Upgraded.png", 9, 8, 212, 168),
        };

        // The one boat origin (amidships, keel bottom, centreline) every punt layer pins to, normalized.
        private const float OriginX = 0.5f;          // 92/184 == 106/212
        private const float OriginY = 74f / 168f;    // (168−94) flipped to bottom-origin

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
            // The punt's sheets all fit under Unity's default 2048 maxTextureSize, so unlike the skiffs'
            // 2448-wide motor sheets they should never trip the downscale trap. Assert it anyway: a
            // downscaled sheet cannot carry a source-pixel grid (rects get refit + alpha-trimmed and the
            // pivot is thrown away) and the sprite COUNT still matches, so this and the pivot test are the
            // only things that would catch it.
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
            Assert.AreEqual(92f / 184f, 106f / 212f, 1e-6f,
                            "hull and motor anchors must normalize identically");

            var hull = LoadSlices("PuntIso.png").First();
            var motor = LoadSlices("PuntMotorUpper-Basic.png").First();

            Assert.AreEqual(hull.pivot.x / hull.rect.width, motor.pivot.x / motor.rect.width, 1e-4f,
                            "hull/motor normalized pivot.x diverged — the outboard will sit off-centre");
            Assert.AreEqual(hull.pivot.y / hull.rect.height, motor.pivot.y / motor.rect.height, 1e-4f,
                            "hull/motor normalized pivot.y diverged — the outboard will float off the transom");
        }

        [Test]
        public void BothPaintBuilds_ShareTheSameCellAndPivot_SoTheyAreDropInSwaps()
        {
            // README: "Both builds share the SAME cell, pivot, steer cols and grip JSON — the sheets are
            // drop-in swaps." A boat instance picks starter vs. upgraded by swapping the two PNGs, so any
            // drift between the builds would shift the engine when the owner upgrades it.
            foreach (var layer in new[] { "Upper", "Lower" })
            {
                var basic = LoadSlices($"PuntMotor{layer}-Basic.png");
                var upgraded = LoadSlices($"PuntMotor{layer}-Upgraded.png");
                Assert.AreEqual(basic.Length, upgraded.Length,
                                $"PuntMotor{layer}: paint builds must have the same slice count");
                for (int i = 0; i < basic.Length; i++)
                {
                    Assert.AreEqual(basic[i].pivot, upgraded[i].pivot,
                                    $"PuntMotor{layer} slice {i}: paint builds must share the pivot");
                    Assert.AreEqual(basic[i].rect.size, upgraded[i].rect.size,
                                    $"PuntMotor{layer} slice {i}: paint builds must share the cell size");
                }
            }
        }

        [Test]
        public void PuntOrigin_IsDistinctFromTheSkiffOrigin()
        {
            // Same anchor *concept* (amidships, keel bottom, centreline), different cell — so the punt
            // needs her OWN const. The skiffs derive (0.5, 96/216 = 0.4444) from a 244×216 cell; the punt
            // derives (0.5, 74/168 = 0.4405) from a 184×168 one. Reusing SkiffOrigin here would sink the
            // punt ~0.7 px at PPU 32. This test exists so a future "tidy-up" that folds the two consts
            // together fails loudly instead of quietly settling the boat.
            const float skiffOriginY = 96f / 216f;
            Assert.AreNotEqual(skiffOriginY, OriginY, 1e-4f,
                               "the punt's origin must be derived from her own cell, not reused from the skiffs'");

            var punt = LoadSlices("PuntIso.png").First();
            Assert.AreEqual(OriginY, punt.pivot.y / punt.rect.height, 1e-4f,
                            "PuntIso pivot drifted off the punt's own README-derived origin");
        }
    }
}
