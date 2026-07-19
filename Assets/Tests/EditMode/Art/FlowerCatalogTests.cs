using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// The flower catalog, checked AGAINST THE ART ON DISK rather than against its own constants. This distinction
    /// is the point of the fixture: a test that asserts <c>GridFor(Single).Cols == 4</c> passes vacuously and would
    /// keep passing after the art director re-exported the sheets at a different grid. So every assertion here
    /// reads the real PNGs and the real sliced sprite rects and holds the catalog to them.
    /// </summary>
    public class FlowerCatalogTests
    {
        // ---- the catalog agrees with the sheets on disk ------------------------------------------------

        [Test]
        public void EverySheetOnDisk_MatchesItsTierGrid_InSizeSliceCountAndCellRects()
        {
            var stems = FlowerCatalog.SheetStems();
            Assert.IsNotEmpty(stems, $"No flower sheets found under {FlowerCatalog.FlowersRoot}. If the art moved, " +
                                     "this fixture is asserting nothing — treat it as a failure.");

            var problems = new List<string>();
            int checkedSheets = 0;

            foreach (string stem in stems)
            {
                if (!FlowerCatalog.TrySplit(stem, out _, out FlowerCatalog.Tier tier))
                {
                    problems.Add($"'{stem}' matches no Single/Clump/Patch tier — the catalog would ignore it.");
                    continue;
                }

                var grid = FlowerCatalog.GridFor(tier);
                string path = FlowerCatalog.SheetPath(stem);

                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex == null) { problems.Add($"'{stem}' did not load as a Texture2D."); continue; }

                // The sheet's real pixel size must be the tier grid's size.
                if (tex.width != grid.Cols * grid.CellW || tex.height != grid.Rows * grid.CellH)
                {
                    problems.Add($"'{stem}' ({tier}) is {tex.width}x{tex.height} on disk but the catalog grid " +
                                 $"says {grid.Cols * grid.CellW}x{grid.Rows * grid.CellH}.");
                    continue;
                }

                // Multiple-mode sheets return null from LoadAssetAtPath<Sprite> — LoadAllAssetsAtPath is the rule.
                var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
                if (sprites.Length != grid.Count)
                {
                    problems.Add($"'{stem}' ({tier}) has {sprites.Length} slices on disk, catalog grid expects " +
                                 $"{grid.Count}. Sliced?");
                    continue;
                }

                // Every slice's rect must be exactly one grid cell, at the row-major position its name claims.
                foreach (var s in sprites)
                {
                    int idx = int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1));
                    int row = idx / grid.Cols, col = idx % grid.Cols;
                    float wantX = col * grid.CellW;
                    float wantY = (grid.Rows - 1 - row) * grid.CellH;   // row 0 = TOP; Unity rects are bottom-origin
                    if (!Mathf.Approximately(s.rect.width, grid.CellW) ||
                        !Mathf.Approximately(s.rect.height, grid.CellH) ||
                        !Mathf.Approximately(s.rect.x, wantX) ||
                        !Mathf.Approximately(s.rect.y, wantY))
                        problems.Add($"'{s.name}' rect is {s.rect} but grid cell (row {row}, col {col}) is " +
                                     $"({wantX},{wantY},{grid.CellW},{grid.CellH}).");
                }
                checkedSheets++;
            }

            Assert.IsEmpty(problems, "The catalog disagrees with the art on disk:\n  " + string.Join("\n  ", problems));
            Assert.AreEqual(stems.Count, checkedSheets, "Some sheets were skipped rather than checked.");
        }

        /// <summary>
        /// THE SHADER'S LOAD-BEARING PRECONDITION, measured off the art. HiddenHarboursFlower.shader shifts poses
        /// with <c>frac(uv.x + k / _Cols)</c>. That lands on the next pose only if a cell is EXACTLY 1/_Cols of the
        /// sheet's width — i.e. the sway columns tile the sheet with no remainder. If a re-export ever adds
        /// padding or a fifth column, the shader silently samples a neighbouring pose's pixels and this catches it.
        /// </summary>
        [Test]
        public void EverySheet_HasCellsExactlyOneOverColsWide_SoTheShadersPoseSelectLands()
        {
            var problems = new List<string>();
            foreach (string stem in FlowerCatalog.SheetStems())
            {
                if (!FlowerCatalog.TrySplit(stem, out _, out FlowerCatalog.Tier tier)) continue;
                var grid = FlowerCatalog.GridFor(tier);
                var sprites = AssetDatabase.LoadAllAssetsAtPath(FlowerCatalog.SheetPath(stem)).OfType<Sprite>();

                foreach (var s in sprites)
                {
                    float texW = s.texture.width;
                    float cellUvW = s.rect.width / texW;
                    float wantUvW = 1f / grid.Cols;
                    if (Mathf.Abs(cellUvW - wantUvW) > 1e-6f)
                        problems.Add($"'{s.name}': a cell is {cellUvW} of the sheet in UV, but the shader shifts " +
                                     $"by exactly {wantUvW} (1/{grid.Cols}).");
                }
            }
            Assert.IsEmpty(problems,
                "A sway pose shift would land off-cell and sample the wrong art:\n  " + string.Join("\n  ", problems));
        }

        /// <summary>
        /// The other half of that precondition: sprite UVs must map DIRECTLY onto the source texture. A
        /// SpriteAtlas that packed these sheets would remap them into an atlas page, cells would stop being
        /// 1/_Cols apart, and the pose select would sample NEIGHBOURING FLOWERS. There is no SpriteAtlas in the
        /// project today; this fails the day someone adds one that catches the flowers.
        /// </summary>
        [Test]
        public void NoSpriteAtlas_PacksTheFlowerSheets()
        {
            var offenders = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:SpriteAtlas"))
            {
                string atlasPath = AssetDatabase.GUIDToAssetPath(guid);
                var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
                if (atlas == null) continue;
                foreach (var packed in UnityEditor.U2D.SpriteAtlasExtensions.GetPackables(atlas))
                {
                    if (packed == null) continue;
                    string p = AssetDatabase.GetAssetPath(packed);
                    if (!string.IsNullOrEmpty(p) && p.StartsWith(FlowerCatalog.FlowersRoot))
                        offenders.Add($"{atlasPath} packs {p}");
                }
            }
            Assert.IsEmpty(offenders,
                "A SpriteAtlas is packing the flower sheets. HiddenHarboursFlower.shader's frac(uv.x + k/_Cols) " +
                "pose select assumes sprite UVs map straight onto the SOURCE texture — atlas-packed, it will " +
                "sample other flowers' pixels. Exclude Art/Foliage/Flowers from the atlas, or replace the pose " +
                "select with a per-sprite rect uniform:\n  " + string.Join("\n  ", offenders));
        }

        // ---- the species scan, including the Lupin hole ------------------------------------------------

        [Test]
        public void Scan_CoversEverySheetOnDisk_WithNothingInventedAndNothingDropped()
        {
            var species = FlowerCatalog.Scan();
            var stemsOnDisk = new HashSet<string>(FlowerCatalog.SheetStems());

            // Every sheet is reachable through some species...
            var reached = new HashSet<string>();
            foreach (var sp in species)
                foreach (FlowerCatalog.Tier t in System.Enum.GetValues(typeof(FlowerCatalog.Tier)))
                    if (sp.Has(t)) reached.Add(sp.SheetFor(t));

            CollectionAssert.AreEquivalent(stemsOnDisk, reached,
                "Every flower sheet on disk must be reachable through the catalog, and the catalog must not " +
                "invent sheets that are not there.");
        }

        [Test]
        public void Scan_FindsTheEightSpeciesAndTheFourLupinColours_AsTheArtActuallyShipped()
        {
            var keys = FlowerCatalog.Scan().Select(s => s.Key).ToList();

            // Derived from the FILES, not typed out: whatever stems exist, minus the tier suffix, minus the
            // shared LupinPatch (which is not a species — see FlowerCatalog's class doc).
            var expected = new HashSet<string>();
            foreach (string stem in FlowerCatalog.SheetStems())
            {
                if (stem == FlowerCatalog.SharedLupinPatchStem) continue;
                if (FlowerCatalog.TrySplit(stem, out string key, out _)) expected.Add(key);
            }

            CollectionAssert.AreEquivalent(expected, keys);
            CollectionAssert.DoesNotContain(keys, "Lupin",
                "'Lupin' is not a species — LupinPatch is the patch the four lupin COLOURS share. Listing it " +
                "would give the owner a phantom lupin with no stem to pick.");
        }

        [Test]
        public void LupinColours_BorrowTheSharedPatch_BecauseTheArtShipsOnlyOne()
        {
            var species = FlowerCatalog.Scan();
            var lupins = species.Where(s => s.Key.StartsWith("Lupin")).ToList();

            Assert.IsNotEmpty(lupins, "No lupin colours found — the Lupin irregularity is unguarded.");

            foreach (var lupin in lupins)
            {
                Assert.IsTrue(lupin.Has(FlowerCatalog.Tier.Single), $"{lupin.Key} should have its own Single.");
                Assert.IsTrue(lupin.Has(FlowerCatalog.Tier.Clump), $"{lupin.Key} should have its own Clump.");
                Assert.IsTrue(lupin.Has(FlowerCatalog.Tier.Patch),
                    $"{lupin.Key} must still resolve a Patch — it borrows the shared one rather than having none.");
                Assert.AreEqual(FlowerCatalog.SharedLupinPatchStem, lupin.SheetFor(FlowerCatalog.Tier.Patch));
                Assert.IsTrue(lupin.PatchIsShared,
                    $"{lupin.Key}'s patch is the shared one and must be FLAGGED as such, so the paint tool can " +
                    "tell the owner why all four lupin colours patch identically.");
            }
        }

        [Test]
        public void NonLupinSpecies_HaveTheirOwnFullSet_AndNeverBorrowAPatch()
        {
            foreach (var sp in FlowerCatalog.Scan().Where(s => !s.Key.StartsWith("Lupin")))
            {
                Assert.IsTrue(sp.Has(FlowerCatalog.Tier.Single), $"{sp.Key} is missing its Single.");
                Assert.IsTrue(sp.Has(FlowerCatalog.Tier.Clump), $"{sp.Key} is missing its Clump.");
                Assert.IsTrue(sp.Has(FlowerCatalog.Tier.Patch), $"{sp.Key} is missing its Patch.");
                Assert.IsFalse(sp.PatchIsShared, $"{sp.Key} has its own patch and must not be flagged as sharing.");
            }
        }

        [TestCase("WildRoseClump", "WildRose", FlowerCatalog.Tier.Clump)]
        [TestCase("LupinBlueSingle", "LupinBlue", FlowerCatalog.Tier.Single)]
        [TestCase("QueenAnnePatch", "QueenAnne", FlowerCatalog.Tier.Patch)]
        public void TrySplit_SeparatesSpeciesFromTier(string stem, string key, FlowerCatalog.Tier tier)
        {
            Assert.IsTrue(FlowerCatalog.TrySplit(stem, out string k, out FlowerCatalog.Tier t));
            Assert.AreEqual(key, k);
            Assert.AreEqual(tier, t);
        }

        [TestCase("Single")]      // a bare suffix is not a species named ""
        [TestCase("GrassTuft")]
        [TestCase("")]
        public void TrySplit_RejectsWhatIsNotATierSheet(string stem)
        {
            Assert.IsFalse(FlowerCatalog.TrySplit(stem, out _, out _));
        }

        // ---- cell loading -----------------------------------------------------------------------------

        [Test]
        public void LoadCell_ReturnsTheSliceItsNameClaims_NotWhateverOrderUnityHandsBack()
        {
            // AssetDatabase.LoadAllAssetsAtPath promises no ordering, so the catalog matches slices by NAME.
            var grid = FlowerCatalog.GridFor(FlowerCatalog.Tier.Single);
            for (int row = 0; row < grid.Rows; row++)
            {
                for (int col = 0; col < grid.Cols; col++)
                {
                    var s = FlowerCatalog.LoadCell("OxeyeDaisySingle", FlowerCatalog.Tier.Single, row, col);
                    Assert.IsNotNull(s, $"OxeyeDaisySingle row {row} col {col} did not load.");
                    Assert.AreEqual($"OxeyeDaisySingle_{row * grid.Cols + col}", s.name);
                }
            }
        }

        [Test]
        public void LoadNeutral_IsColumnZero_TheShadersPhaseZeroPose()
        {
            var neutral = FlowerCatalog.LoadNeutral("OxeyeDaisySingle", FlowerCatalog.Tier.Single, row: 1);
            var col0 = FlowerCatalog.LoadCell("OxeyeDaisySingle", FlowerCatalog.Tier.Single, row: 1, col: 0);
            Assert.AreSame(col0, neutral);
        }

        [Test]
        public void LoadCell_ReturnsNullOutsideTheGrid_RatherThanThrowing()
        {
            // A partial art drop must degrade, not explode: the builder relies on this to skip and warn.
            Assert.IsNull(FlowerCatalog.LoadCell("OxeyeDaisySingle", FlowerCatalog.Tier.Single, row: 3, col: 0));
            Assert.IsNull(FlowerCatalog.LoadCell("OxeyeDaisySingle", FlowerCatalog.Tier.Single, row: 0, col: 4));
            Assert.IsNull(FlowerCatalog.LoadCell("NoSuchFlowerSingle", FlowerCatalog.Tier.Single, row: 0, col: 0));
        }
    }
}
