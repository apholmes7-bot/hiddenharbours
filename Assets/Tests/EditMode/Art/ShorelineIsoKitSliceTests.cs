using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Guards the import of the two TERRAIN kits — the shoreline-ISO tile kit under
    /// <c>Art/Tilesets/ShorelineIso/</c> and the road/path blob-47 atlases under
    /// <c>Art/Tilesets/Roads/</c>.
    ///
    /// <para>Both kits are 32×32 cells at 32 px = 1 m, and the shoreline kit is baked to the SAME
    /// camera as the boat turntables (¾ from the south at 40°) so land and hull sit in one space.
    /// Every dimension, row map and column map below is restated as a LITERAL from the kit READMEs and
    /// <c>ShorelineIso.json</c> as delivered — deliberately NOT imported from
    /// <see cref="ShorelineIsoCatalog"/> or read back out of the live sidecar. Asserting the catalog
    /// against the catalog is the self-referential blind spot that let mirrored boat art ship; these
    /// literals pin the contract from the outside, so a drifted re-bake, an edited sidecar and a
    /// mistyped catalog each fail loudly and each failure points at its own file.</para>
    ///
    /// <para><b>No water is asserted anywhere, because the kits bake none</b> (ADR 0010/0012/0023).
    /// The shader owns the waterline, foam and swash; the tide sweeps over whatever ground is painted.
    /// A test that expected a foam or wet-edge tile here would be testing a contract that does not
    /// exist.</para>
    ///
    /// <para>Nothing here touches the GPU, so no Null-device gate is needed. The slice-state checks are
    /// skipped (not failed) while a sheet is still Single-mode: the art lands in one PR and the slice
    /// is applied by an editor menu the owner runs, so failing on "not sliced yet" would redden main
    /// for a step that is by design manual. The moment a sheet IS sliced, the counts and pivots are
    /// asserted hard.</para>
    /// </summary>
    public class ShorelineIsoKitSliceTests
    {
        private const string ShoreDir = "Assets/_Project/Art/Tilesets/ShorelineIso/";
        private const string RoadDir  = "Assets/_Project/Art/Tilesets/Roads/";
        private const string SidecarPath = ShoreDir + "ShoreIsoSprites.json";
        private const string ContractPath = ShoreDir + "ShorelineIso.json";
        private const string ShoreRigPath = "docs/art/rigs/shoreIsoKitRig.js";
        private const string RoadRigPath  = "docs/art/rigs/roadPathRig.js";

        /// <summary>The kit grid: one cell is 32 px is 1 m at the locked PPU (ADR 0006/0022).</summary>
        private const int Cell = 32;

        // ---- the kit contract, restated as literals ------------------------------------------

        /// <summary>Uniform-grid sheets: stem → (cols, rows), from the READMEs' stated sheet sizes.</summary>
        private static readonly Dictionary<string, Vector2Int> Grids = new Dictionary<string, Vector2Int>
        {
            // Shoreline ISO — ground fill, terrain-type fringes, cliff bands, the dune bank.
            [ShoreDir + "ShoreIsoGround.png"] = new Vector2Int(3, 6),   //  96×192
            [ShoreDir + "ShoreIsoFringe.png"] = new Vector2Int(12, 3),  // 384×96
            [ShoreDir + "ShoreIsoCliff.png"]  = new Vector2Int(10, 3),  // 320×96
            [ShoreDir + "ShoreIsoDune.png"]   = new Vector2Int(9, 1),   // 288×32

            // Road / path — one pre-baked blob-47 atlas per surface, 12×4 = 48 cells (47 + 1 spare).
            [RoadDir + "RoadIso_dirt_new_blob47.png"]     = new Vector2Int(12, 4),
            [RoadDir + "RoadIso_gravel_new_blob47.png"]   = new Vector2Int(12, 4),
            [RoadDir + "RoadIso_concrete_new_blob47.png"] = new Vector2Int(12, 4),
            [RoadDir + "RoadIso_asphalt_new_blob47.png"]  = new Vector2Int(12, 4),
            [RoadDir + "RoadIso_cobble_new_blob47.png"]   = new Vector2Int(12, 4),
            [RoadDir + "RoadIso_sand_new_blob47.png"]     = new Vector2Int(12, 4),
            [RoadDir + "RoadIso_brick_new_blob47.png"]    = new Vector2Int(12, 4),
        };

        /// <summary>Ground rows, top to bottom, from <c>ShorelineIso.json</c>.</summary>
        private static readonly string[] GroundRows =
            { "grass", "marram", "sand", "ripple", "shingle", "shelf" };

        /// <summary>Fringe rows (only the three soft materials carry a ragged tongue).</summary>
        private static readonly string[] FringeRows = { "grass", "marram", "sand" };

        /// <summary>Fringe columns: 4 edge · 4 outer corner · 4 inner corner.</summary>
        private static readonly string[] FringeCols =
        {
            "edN", "edE", "edS", "edW",
            "coNE", "coSE", "coSW", "coNW",
            "inNE", "inSE", "inSW", "inNW",
        };

        /// <summary>Cliff rows: a cliff of any height is cap + mid×N + toe.</summary>
        private static readonly string[] CliffRows = { "cap", "mid", "toe" };

        /// <summary>Cliff columns; <c>caveToe</c> is the arch and is cliff-only.</summary>
        private static readonly string[] CliffCols =
        {
            "faceS", "cornSW", "cornSE", "sideW", "sideE",
            "innSW", "innSE", "diagSW", "diagSE", "caveToe",
        };

        /// <summary>The seven road surfaces the kit bakes.</summary>
        private static readonly string[] RoadSurfaces =
            { "dirt", "gravel", "concrete", "asphalt", "cobble", "sand", "brick" };

        /// <summary>
        /// The packed rock sheet's items, verbatim from <c>ShoreIsoSprites.json</c> as delivered:
        /// name, rect (x,y,w,h with y from the sheet's TOP), pivot (px,py from the ITEM's top-left).
        /// Four sea stacks then three slab boulders.
        /// </summary>
        private static readonly (string name, int x, int y, int w, int h, int px, int py)[] RockItems =
        {
            ("reef",   4, 34, 24, 10, 12,  9),
            ("s",     32, 30, 16, 14,  8, 13),
            ("m",     52, 22, 22, 22, 11, 21),
            ("l",     78, 14, 30, 30, 15, 29),
            ("bs",   112, 35, 14,  9,  7,  8),
            ("bm",   130, 32, 20, 12, 10, 11),
            ("bl",   154, 28, 28, 16, 14, 15),
        };

        private const int RockSheetW = 186;
        private const int RockSheetH = 44;

        private static string RepoPath(string relative) =>
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, relative);

        private static Sprite[] SlicesOf(string assetPath) =>
            AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Sprite>().ToArray();

        // ---- the art is actually on disk, at the size the contract claims ---------------------

        [Test]
        public void EveryKitSheet_IsOnDisk()
        {
            foreach (var path in Grids.Keys)
                Assert.IsTrue(File.Exists(path), $"'{path}' is missing — the kit import is incomplete.");

            Assert.IsTrue(File.Exists(ShoreDir + "ShoreIsoSprites.png"),
                          "The packed rock sheet is missing.");
            Assert.IsTrue(File.Exists(SidecarPath), "ShoreIsoSprites.json (the rect/pivot sidecar) is missing.");
            Assert.IsTrue(File.Exists(ContractPath), "ShorelineIso.json (the kit contract) is missing.");
        }

        [Test]
        public void EveryUniformSheet_MatchesItsCellGrid()
        {
            foreach (var kv in Grids)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(kv.Key);
                Assert.IsNotNull(tex, $"'{kv.Key}' failed to load as a Texture2D.");

                // A sheet over the importer's cap imports SILENTLY DOWNSCALED, and a downscale keeps the
                // sprite COUNT correct — only a dimension assert like this one catches it. Both kits sit
                // far under 2048, so a failure here means the export drifted, not the cap.
                Assert.AreEqual(kv.Value.x * Cell, tex.width,  $"'{kv.Key}' width");
                Assert.AreEqual(kv.Value.y * Cell, tex.height, $"'{kv.Key}' height");
            }
        }

        [Test]
        public void PackedRockSheet_IsTheSizeTheSidecarAssumes()
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(ShoreDir + "ShoreIsoSprites.png");
            Assert.IsNotNull(tex, "ShoreIsoSprites.png failed to load as a Texture2D.");
            Assert.AreEqual(RockSheetW, tex.width,  "packed rock sheet width");
            Assert.AreEqual(RockSheetH, tex.height, "packed rock sheet height");
        }

        [Test]
        public void BothRigSources_AreVersionedInTheRepo()
        {
            // The rigs are the bake source of truth (ADR 0021): the PNGs are one bake of them, and any
            // re-bake — other wear states, other verges, a taller cliff — runs from these files.
            Assert.IsTrue(File.Exists(RepoPath(ShoreRigPath)), $"'{ShoreRigPath}' is missing.");
            Assert.IsTrue(File.Exists(RepoPath(RoadRigPath)),  $"'{RoadRigPath}' is missing.");
        }

        // ---- the live sidecar still says what the literals above say --------------------------

        [Test]
        public void Sidecar_MatchesTheKitContractLiterals()
        {
            var json = File.ReadAllText(SidecarPath);
            var parsed = JsonUtility.FromJson<SidecarProbe>(json);

            Assert.IsNotNull(parsed.items, "ShoreIsoSprites.json parsed to no items.");
            Assert.AreEqual(RockItems.Length, parsed.items.Length, "rock item count");

            for (int i = 0; i < RockItems.Length; i++)
            {
                var want = RockItems[i];
                var got = parsed.items[i];
                Assert.AreEqual(want.name, got.name, $"item {i} name");
                Assert.AreEqual(want.x, got.x, $"'{want.name}' x");
                Assert.AreEqual(want.y, got.y, $"'{want.name}' y");
                Assert.AreEqual(want.w, got.w, $"'{want.name}' w");
                Assert.AreEqual(want.h, got.h, $"'{want.name}' h");
                Assert.AreEqual(want.px, got.pivot[0], $"'{want.name}' pivot x");
                Assert.AreEqual(want.py, got.pivot[1], $"'{want.name}' pivot y");
            }
        }

        // ---- the top-left → bottom-left flip ---------------------------------------------------

        [Test]
        public void RockRects_FlipFromTheSidecarsTopLeftSpaceIntoUnitys()
        {
            var items = RockItems.Select(r => new ShorelineIsoSpriteSlicer.SidecarItem
            {
                name = r.name, x = r.x, y = r.y, w = r.w, h = r.h, pivot = new[] { r.px, r.py },
            }).ToList();

            var rects = ShorelineIsoSpriteSlicer.BuildRects(items, RockSheetH);
            Assert.AreEqual(RockItems.Length, rects.Length);

            for (int i = 0; i < RockItems.Length; i++)
            {
                var want = RockItems[i];
                var got = rects[i];

                Assert.AreEqual($"ShoreIsoSprites_{want.name}", got.name, $"item {i} slice name");
                Assert.AreEqual((float)want.x, got.rect.x, 1e-4f, $"'{want.name}' rect x is unchanged by the flip");
                Assert.AreEqual((float)(RockSheetH - (want.y + want.h)), got.rect.y, 1e-4f,
                                $"'{want.name}' rect y must flip to bottom-origin");
                Assert.AreEqual((float)want.w, got.rect.width,  1e-4f, $"'{want.name}' rect width");
                Assert.AreEqual((float)want.h, got.rect.height, 1e-4f, $"'{want.name}' rect height");

                Assert.AreEqual(want.px / (float)want.w, got.pivot.x, 1e-5f, $"'{want.name}' pivot x");
                Assert.AreEqual((want.h - want.py) / (float)want.h, got.pivot.y, 1e-5f,
                                $"'{want.name}' pivot y must flip and normalize");
            }
        }

        [Test]
        public void EveryRockSprite_PivotsOnItsBase_OneRowUpFromTheBottom()
        {
            // The kit's whole placement contract in one line: a stack dropped at a world position must
            // PLANT there, not float. Every item's pivot is horizontally centred and sits exactly 1 px
            // above its own base — that is the "base-centre contact point" the README describes, and it
            // is what lets a boulder and a sea stack of different heights be placed by the same rule.
            foreach (var it in RockItems)
            {
                Assert.AreEqual(it.w / 2, it.px, $"'{it.name}' pivot must be horizontally centred");
                Assert.AreEqual(it.h - 1, it.py, $"'{it.name}' pivot must sit on its base row");
            }
        }

        // ---- the catalog resolves the contract's rows and columns ------------------------------

        [Test]
        public void Catalog_RowAndColumnMaps_MatchTheContract()
        {
            CollectionAssert.AreEqual(GroundRows,  ShorelineIsoCatalog.GroundMaterials, "ground rows");
            CollectionAssert.AreEqual(FringeRows,  ShorelineIsoCatalog.FringeMaterials, "fringe rows");
            CollectionAssert.AreEqual(FringeCols,  ShorelineIsoCatalog.FringePieces,    "fringe columns");
            CollectionAssert.AreEqual(CliffRows,   ShorelineIsoCatalog.CliffBands,      "cliff rows");
            CollectionAssert.AreEqual(CliffCols,   ShorelineIsoCatalog.CliffPieces,     "cliff columns");
            CollectionAssert.AreEqual(RoadSurfaces, ShorelineIsoCatalog.RoadSurfaces,   "road surfaces");

            // The dune is the cliff's nine landform pieces WITHOUT the cave — a dune has no arch.
            CollectionAssert.AreEqual(CliffCols.Take(9).ToArray(), ShorelineIsoCatalog.DunePieces,
                                      "dune pieces = cliff pieces minus caveToe");
            CollectionAssert.DoesNotContain(ShorelineIsoCatalog.DunePieces, "caveToe");
        }

        [Test]
        public void Catalog_IndexesRowMajorFromTheTopLeft()
        {
            // Row-major from the top-left is the slicer's naming order; these spot-checks pin the two
            // ends and one interior cell of each sheet so an off-by-a-row cannot pass.
            Assert.AreEqual(0, ShorelineIsoCatalog.GroundIndex("grass"), "grass is the top row");
            Assert.AreEqual(2, ShorelineIsoCatalog.GroundIndex("grass", 2), "third variant of the top row");
            Assert.AreEqual(6, ShorelineIsoCatalog.GroundIndex("sand"), "sand is row 2 of 3 columns");
            Assert.AreEqual(17, ShorelineIsoCatalog.GroundIndex("shelf", 2), "the last ground cell");

            Assert.AreEqual(0, ShorelineIsoCatalog.FringeIndex("grass", "edN"));
            Assert.AreEqual(24, ShorelineIsoCatalog.FringeIndex("sand", "edN"), "sand is row 2 of 12 columns");
            Assert.AreEqual(35, ShorelineIsoCatalog.FringeIndex("sand", "inNW"), "the last fringe cell");

            Assert.AreEqual(0, ShorelineIsoCatalog.CliffIndex("cap", "faceS"));
            Assert.AreEqual(10, ShorelineIsoCatalog.CliffIndex("mid", "faceS"), "mid is row 1 of 10 columns");
            Assert.AreEqual(29, ShorelineIsoCatalog.CliffIndex("toe", "caveToe"),
                            "the cave arch is the last cell of the toe row");

            Assert.AreEqual(0, ShorelineIsoCatalog.DuneIndex("faceS"));
            Assert.AreEqual(8, ShorelineIsoCatalog.DuneIndex("diagSE"), "the last dune cell");
        }

        [Test]
        public void Catalog_RejectsAnUnknownMaterialOrPiece()
        {
            // Reaching for a tile the kit doesn't bake must throw where the mistake is, not hand back
            // index 0 and paint grass where a cliff was meant.
            Assert.Throws<System.ArgumentOutOfRangeException>(() => ShorelineIsoCatalog.GroundIndex("foam"));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => ShorelineIsoCatalog.CliffIndex("mid", "faceN"));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => ShorelineIsoCatalog.DuneIndex("caveToe"));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => ShorelineIsoCatalog.RoadAtlasPath("tarmac"));
        }

        [Test]
        public void Catalog_StopsAtTheBlobSet_NotTheAtlasRectangle()
        {
            // 12×4 = 48 cells hold 47 blob tiles, so the last cell is padding. Anything that walked the
            // atlas by its rectangle would paint that spare cell as a road.
            Assert.AreEqual(47, ShorelineIsoCatalog.RoadBlobCount);
            Assert.AreEqual(48, ShorelineIsoCatalog.RoadCols * ShorelineIsoCatalog.RoadRows,
                            "the atlas is one cell larger than the blob set — that is the padding cell");
        }

        // ---- once the sheets are sliced, the slice must match the grid -------------------------

        [Test]
        public void SlicedSheets_HaveOneSpritePerCell()
        {
            int checkedSheets = 0;

            foreach (var kv in Grids)
            {
                var importer = AssetImporter.GetAtPath(kv.Key) as TextureImporter;
                Assert.IsNotNull(importer, $"'{kv.Key}' has no TextureImporter.");
                if (importer.spriteImportMode != SpriteImportMode.Multiple) continue;

                checkedSheets++;
                int expected = kv.Value.x * kv.Value.y;
                var slices = SlicesOf(kv.Key);
                Assert.AreEqual(expected, slices.Length,
                                $"'{kv.Key}' should slice to {kv.Value.x}×{kv.Value.y} cells.");

                foreach (var s in slices)
                {
                    Assert.AreEqual((float)Cell, s.rect.width,  0.01f, $"'{s.name}' cell width");
                    Assert.AreEqual((float)Cell, s.rect.height, 0.01f, $"'{s.name}' cell height");
                    // Centre pivot: a tilemap places by cell, so any other pivot shifts a painted tile
                    // off the cell it was painted into — and a stacked cliff band off its neighbour.
                    Assert.AreEqual(Cell / 2f, s.pivot.x, 0.01f, $"'{s.name}' pivot x");
                    Assert.AreEqual(Cell / 2f, s.pivot.y, 0.01f, $"'{s.name}' pivot y");
                }
            }

            if (checkedSheets == 0)
                Assert.Ignore("No terrain sheet is sliced yet — run " +
                              "Hidden Harbours ▸ Art ▸ Slice Environment + VFX Sheets in the editor.");
        }

        [Test]
        public void SlicedRockSheet_CarriesTheSidecarsNamesAndBasePivots()
        {
            const string sheet = ShoreDir + "ShoreIsoSprites.png";
            var importer = AssetImporter.GetAtPath(sheet) as TextureImporter;
            Assert.IsNotNull(importer, "ShoreIsoSprites.png has no TextureImporter.");

            if (importer.spriteImportMode != SpriteImportMode.Multiple)
                Assert.Ignore("The rock sheet is not sliced yet — run " +
                              "Hidden Harbours ▸ Art ▸ Slice Shoreline Iso Rock Sprites in the editor.");

            var byName = SlicesOf(sheet).ToDictionary(s => s.name);
            Assert.AreEqual(RockItems.Length, byName.Count, "rock sprite count");

            foreach (var it in RockItems)
            {
                string name = $"ShoreIsoSprites_{it.name}";
                Assert.IsTrue(byName.TryGetValue(name, out var s), $"'{name}' is missing from the slice.");
                Assert.AreEqual((float)it.w, s.rect.width,  0.01f, $"'{name}' width");
                Assert.AreEqual((float)it.h, s.rect.height, 0.01f, $"'{name}' height");
                Assert.AreEqual(it.w / 2f, s.pivot.x, 0.01f, $"'{name}' pivot must be centred");
                Assert.AreEqual(1f, s.pivot.y, 0.01f, $"'{name}' pivot must sit 1 px above its base");
            }
        }

        // ---- JsonUtility probes ----------------------------------------------------------------

        [System.Serializable]
        private struct SidecarProbe
        {
            public string sheet;
            public ProbeItem[] items;
        }

        [System.Serializable]
        private struct ProbeItem
        {
            public string name;
            public int x, y, w, h;
            public int[] pivot;
        }
    }
}
