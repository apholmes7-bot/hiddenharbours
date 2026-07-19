using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Guards the baked slice of the Cape Islander iso kit (the ~12.9 m inshore working boat and her
    /// 8-frame wave-rock loop). Mirrors <see cref="SkiffSheetSliceTests"/> and
    /// <see cref="PuntSheetSliceTests"/> for the biggest hull in the project.
    ///
    /// <para><b>THE DOWNSCALE TRAP IS REAL ON THIS KIT, not theoretical.</b> Her rock sheet is
    /// 3648×3360 — BOTH dimensions past Unity's default 2048 <c>maxTextureSize</c> — so without the
    /// cap lift it imports at 0.56× and every source-pixel rect is refit, alpha-trimmed, and stripped
    /// of its pivot. The punt's sheets all fit under 2048 and could never trip it; the skiffs' motor
    /// sheets did, and the failure was silent. It is silent because <b>the sprite COUNT still comes out
    /// right</b>: 8 and 64, exactly as expected, on a sheet that is now 2048×1886 of mush. Only
    /// <see cref="Sheet_ImportsAtNativeRes_NotDownscaled"/> and the cell-size/pivot assertions in
    /// <see cref="EverySlice_SharesTheBoatOriginPivot"/> can tell the difference, which is why the cell
    /// rect is asserted explicitly here and not merely implied by the count.</para>
    ///
    /// <para><b>The pivot is the one MEASURED number in this kit.</b> Every other iso kit shipped a
    /// README fixing its anchor; the Cape Islander arrived as two loose PNGs with neither README nor
    /// rig, so hers was recovered from the pixels — see the derivation on
    /// <c>SpriteSheetSlicer.CapeIslanderOrigin</c>, which also records the ≈±4 px (≈0.12 m) uncertainty
    /// that recovery carries. This fixture pins what was measured so a re-slice cannot quietly move it,
    /// and <see cref="CapeIslanderOrigin_IsHerOwn_NotBorrowedFromAnotherKit"/> stops a future tidy-up
    /// from folding her into a neighbour's const the way the punt was nearly folded into the skiffs'.</para>
    /// </summary>
    public class CapeIslanderSheetSliceTests
    {
        private const string Boats = "Assets/_Project/Art/Boats/";

        // The measured kit geometry, as expectations: (file, cols, rows, cellW, cellH).
        //   Hull — 8 cols × 1 ROW  → index = heading (0 N, 1 NE, 2 E, 3 SE, 4 S, 5 SW, 6 W, 7 NW as
        //          LABELLED; the art is baked counter-clockwise — see CapeIslanderFacingTests).
        //   Rock — 8 cols (wave frame) × 8 ROWS (heading) → index = heading×8 + frame.
        // NOTE THE AXIS FLIP: the base sheet's columns are facings, the rock sheet's ROWS are. Every iso
        // kit in the project does this, and row-major slicing is what turns it into the index contract.
        private static readonly (string File, int Cols, int Rows, int CellW, int CellH)[] Sheets =
        {
            ("CapeIslanderIso.png",     8, 1, 456, 420),
            ("CapeIslanderIsoRock.png", 8, 8, 456, 420),
        };

        // Her boat origin (amidships, keel bottom, centreline), normalized. (228, 263) from the cell's
        // TOP-LEFT; flipped to Unity's bottom-left origin, y = (420 − 263) = 157.
        private const float OriginX = 0.5f;           // 228/456 — the cardinal cells are mirror-symmetric here
        private const float OriginY = 157f / 420f;    // ≈0.373809

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
        public void Sheet_LiftsTheSizeCap_SoItCanImportAtNativeRes(
            (string File, int Cols, int Rows, int CellW, int CellH) s)
        {
            // The cap itself, asserted on the importer rather than inferred from the result — so the
            // reason a future regression happens is legible, not just the symptom. 3648 needs 4096; the
            // 2048 default would silently halve the sheet. SpriteSheetSlicer lifts this automatically,
            // and this is the assertion that says it must stay lifted.
            var importer = AssetImporter.GetAtPath(Boats + s.File) as TextureImporter;
            Assert.IsNotNull(importer, $"{s.File}: no TextureImporter");
            int needed = Mathf.NextPowerOfTwo(Mathf.Max(s.Cols * s.CellW, s.Rows * s.CellH));
            Assert.GreaterOrEqual(importer.maxTextureSize, needed,
                $"{s.File}: the sheet is {s.Cols * s.CellW}×{s.Rows * s.CellH} but maxTextureSize is " +
                $"{importer.maxTextureSize} — it needs at least {needed}. Below that Unity DOWNSCALES the " +
                "texture on import and the grid slice becomes garbage, while the sprite COUNT still looks " +
                "right. Re-run Hidden Harbours ▸ Art ▸ Slice Environment + VFX Sheets.");
        }

        [Test]
        [TestCaseSource(nameof(AllSheets))]
        public void Sheet_ImportsAtNativeRes_NotDownscaled(
            (string File, int Cols, int Rows, int CellW, int CellH) s)
        {
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
            // Pixels, not normalized: a heading- or frame-swap must never shift the boat. The CELL RECT is
            // asserted alongside the pivot on purpose — it is the assertion that survives a downscale even
            // if a future importer change happens to preserve the normalized pivot.
            float expX = OriginX * s.CellW;
            float expY = OriginY * s.CellH;
            foreach (var sprite in LoadSlices(s.File))
            {
                Assert.AreEqual(s.CellW, sprite.rect.width, 0.01f,
                    $"{sprite.name}: cell width is {sprite.rect.width}, not {s.CellW}. If it is ~0.56× that, " +
                    "the sheet imported DOWNSCALED past maxTextureSize — the count would still be right.");
                Assert.AreEqual(s.CellH, sprite.rect.height, 0.01f,
                    $"{sprite.name}: cell height is {sprite.rect.height}, not {s.CellH} — see above");
                Assert.AreEqual(expX, sprite.pivot.x, 0.01f, $"{sprite.name}: pivot.x off the boat origin");
                Assert.AreEqual(expY, sprite.pivot.y, 0.01f, $"{sprite.name}: pivot.y off the boat origin");
            }
        }

        [Test]
        [TestCaseSource(nameof(AllSheets))]
        public void Slices_AreNamed_StemUnderscoreIndex_ContiguousFromZero(
            (string File, int Cols, int Rows, int CellW, int CellH) s)
        {
            // The `_N` suffix IS the index math contract (heading = index/Cols, frame = index%Cols) and is
            // what BoatVisualLibraryBuilder.SpriteIndex parses. A gap or a rename silently mis-maps headings.
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
        public void HullAndRock_ShareTheSameCellAndPivot_SoTheRockNeverShiftsTheBoat()
        {
            // The rock grid replaces the static facing frame-by-frame under the wave. If the two sheets
            // disagreed by even a pixel the whole boat would twitch every time the wave phase crossed a
            // frame boundary — a very visible bug for a very small cause.
            var hull = LoadSlices("CapeIslanderIso.png").First();
            var rock = LoadSlices("CapeIslanderIsoRock.png").First();

            Assert.AreEqual(hull.rect.size, rock.rect.size, "hull and rock cells must match exactly");
            Assert.AreEqual(hull.pivot.x, rock.pivot.x, 1e-4f, "hull/rock pivot.x diverged");
            Assert.AreEqual(hull.pivot.y, rock.pivot.y, 1e-4f, "hull/rock pivot.y diverged");
        }

        [Test]
        public void CapeIslanderOrigin_IsHerOwn_NotBorrowedFromAnotherKit()
        {
            // Same anchor *concept* (amidships, keel bottom, centreline), a very different cell — so she
            // needs her OWN const. The dory derives 68/156 = 0.4359, the punt 74/168 = 0.4405, the skiffs
            // 96/216 = 0.4444; hers is 157/420 ≈ 0.3738, which is not close to any of them. This test
            // exists so a future "tidy-up" that folds the consts together fails loudly rather than
            // quietly sinking a 13 m boat by nearly a metre.
            foreach (var (name, y) in new[] { ("dory", 68f / 156f), ("punt", 74f / 168f), ("skiff", 96f / 216f) })
                Assert.That(OriginY, Is.Not.EqualTo(y).Within(1e-3f),
                            $"the Cape Islander's origin must be her own, not the {name}'s");

            var hull = LoadSlices("CapeIslanderIso.png").First();
            Assert.AreEqual(OriginY, hull.pivot.y / hull.rect.height, 1e-4f,
                            "CapeIslanderIso pivot drifted off her measured origin");
        }

        [Test]
        public void HerCellIsTheBiggestInTheProject_AndThatIsAMemoryFact_NotAnAccident()
        {
            // Not a style assertion — a budget one (CLAUDE.md rule 7, "mind texture memory"). Her rock
            // sheet alone is 3648×3360 RGBA32 = ~46.8 MiB uncompressed, roughly 8× the dory's whole kit.
            // If someone later re-exports her at a larger cell this goes red and the cost gets re-argued
            // rather than absorbed.
            var rock = AssetDatabase.LoadAssetAtPath<Texture2D>(Boats + "CapeIslanderIsoRock.png");
            Assert.IsNotNull(rock);
            double mib = (double)rock.width * rock.height * 4 / (1024 * 1024);
            Assert.Less(mib, 50d,
                $"CapeIslanderIsoRock is {mib:0.0} MiB uncompressed RGBA. That is already the largest single " +
                "texture in the project; past 50 it needs an explicit decision (compression trades away the " +
                "crisp pixel edges), not a silent import.");
        }
    }
}
