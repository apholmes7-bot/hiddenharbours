using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Guards the import of the shoreline-ISO <b>v8</b> tile kit (<c>ShoreIso2</c>) under
    /// <c>Art/Tilesets/ShorelineIso2/&lt;style&gt;/</c> — five sheets each in <c>nat</c> and
    /// <c>gfx</c>, plus the per-style <c>ShorelineIso.json</c> contract they ship with.
    ///
    /// <para><b>Every fact is checked on three legs, and that is the point.</b> The drop's README is
    /// restated here as LITERALS; the live <c>ShorelineIso.json</c> is re-read from disk on every
    /// run; the imported PNG is measured through the AssetDatabase. Asserting any one of those
    /// against itself is the self-referential blind spot that let mirrored boat art ship — checking
    /// literal ⟷ contract ⟷ asset means a drifted re-bake, a hand-edited sidecar and a mistyped
    /// catalog each fail loudly, and each failure names its own file.</para>
    ///
    /// <para><b>No water is asserted anywhere, because the kit bakes none</b> (ADR 0010/0012/0023).
    /// That is not taken on trust: all ten delivered sheets were scanned at import and carry zero
    /// blue-dominant pixels. <see cref="EveryContract_PromisesZeroBakedWater"/> pins the promise;
    /// the shader owns the waterline, foam, swash and the tide-pool fill.</para>
    ///
    /// <para>Only <see cref="CliffPaddingCells_AreActuallyEmpty"/> touches pixels, and it decodes the
    /// PNG's own bytes rather than reading the imported texture — no graphics API, so it is safe on
    /// CI's null device and needs no gate. Nothing else here goes near the GPU.</para>
    /// </summary>
    public class ShorelineIso2KitSliceTests
    {
        // =====================================================================================
        // the drop's README, restated as literals — leg 1 of 3
        // =====================================================================================

        private const string KitRoot = "Assets/_Project/Art/Tilesets/ShorelineIso2";
        private const string RigPath = "docs/art/rigs/shoreIsoKitRig2.js";
        private const int Cell = 32;

        /// <summary>The kit ships twice — same geometry, different shading law.</summary>
        private static readonly string[] Styles = { "nat", "gfx" };

        /// <summary>Sheet stem → (cols, rows), from the README's stated sheet sizes.</summary>
        private static readonly Dictionary<string, Vector2Int> Grids = new Dictionary<string, Vector2Int>
        {
            ["Ground"]  = new Vector2Int(4, 6),   // 128×192
            ["Fringe"]  = new Vector2Int(12, 3),  // 384×96
            ["Cliff"]   = new Vector2Int(10, 3),  // 320×96
            ["Dune"]    = new Vector2Int(9, 2),   // 288×64
            ["Contact"] = new Vector2Int(5, 1),   // 160×32
        };

        private static readonly string[] GroundRows =
            { "grass", "marram", "sand", "ripple", "shingle", "shelf" };

        private static readonly string[] FringeRows = { "grass", "marram", "sand" };

        private static readonly string[] FringeCols =
        {
            "edN", "edE", "edS", "edW",
            "coNE", "coSE", "coSW", "coNW",
            "inNE", "inSE", "inSW", "inNW",
        };

        private static readonly string[] CliffRows = { "cap", "mid", "toe" };

        /// <summary>⚠ The tenth column is <c>toe+cave</c> — filled on the TOE row only.</summary>
        private static readonly string[] CliffCols =
        {
            "faceS", "cornSW", "cornSE", "sideW", "sideE",
            "innSW", "innSE", "diagSW", "diagSE", "toe+cave",
        };

        /// <summary>⚠ v7's dune was ONE band; v8's stacks cap over toe.</summary>
        private static readonly string[] DuneRows = { "cap", "toe" };

        private static readonly string[] DuneCols =
        {
            "faceS", "cornSW", "cornSE", "sideW", "sideE",
            "innSW", "innSE", "diagSW", "diagSE",
        };

        /// <summary>The new-in-v8 occlusion overlays. All northern — the camera looks from the south.</summary>
        private static readonly string[] ContactCols = { "n", "ne", "e", "nw", "w" };

        /// <summary>The two transparent padding cells of <c>Cliff.png</c>: cap+cave and mid+cave.</summary>
        private static readonly int[] CliffPadding = { 9, 19 };

        // =====================================================================================
        // helpers
        // =====================================================================================

        private static string SheetPath(string style, string stem) => $"{KitRoot}/{style}/{stem}.png";
        private static string ContractPath(string style) => $"{KitRoot}/{style}/ShorelineIso.json";

        private static string RepoPath(string relative) =>
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, relative);

        private static Sprite[] SlicesOf(string assetPath) =>
            AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Sprite>().ToArray();

        private static Dictionary<string, object> Contract(string style)
        {
            var c = ShorelineIso2Catalog.LoadContract(style);
            Assert.IsNotNull(c, $"'{ContractPath(style)}' failed to parse.");
            return c;
        }

        /// <summary>
        /// The dimension bar, as a pure function so the sabotage arm can feed it a wrong size.
        /// Returns null when the texture matches the grid, or a description of the mismatch.
        /// </summary>
        private static string GridMismatch(int texW, int texH, Vector2Int grid)
        {
            int wantW = grid.x * Cell, wantH = grid.y * Cell;
            if (texW == wantW && texH == wantH) return null;
            return $"{texW}×{texH} but the contract's {grid.x}×{grid.y} grid needs {wantW}×{wantH}";
        }

        /// <summary>The pivot bar, likewise pure: a tilemap places by cell, so the centre is the only
        /// pivot that keeps a stacked cliff band on the cell it was painted into.</summary>
        private static bool PivotIsCellCentre(Vector2 pivotPx, float tol = 0.01f) =>
            Mathf.Abs(pivotPx.x - Cell / 2f) <= tol && Mathf.Abs(pivotPx.y - Cell / 2f) <= tol;

        // =====================================================================================
        // 1. the art and its contract are actually here
        // =====================================================================================

        [Test]
        public void EverySheetAndContract_IsOnDisk()
        {
            foreach (string style in Styles)
            {
                Assert.IsTrue(File.Exists(ContractPath(style)),
                              $"'{ContractPath(style)}' is missing — the kit import is incomplete.");
                foreach (string stem in Grids.Keys)
                    Assert.IsTrue(File.Exists(SheetPath(style, stem)),
                                  $"'{SheetPath(style, stem)}' is missing.");
            }
        }

        [Test]
        public void TheRigSource_IsVersionedInTheRepo()
        {
            // The rig is the bake source of truth (ADR 0021): the PNGs are one bake of it, and any
            // re-bake — another material, a taller cliff, a third style — runs from this file.
            Assert.IsTrue(File.Exists(RepoPath(RigPath)), $"'{RigPath}' is missing.");
        }

        [Test]
        public void EveryContract_NamesTheKitAndItsOwnStyle()
        {
            // A v9 drop landing in the v8 folder would otherwise import silently and paint a grid
            // nobody checked.
            foreach (string style in Styles)
            {
                var c = Contract(style);
                Assert.AreEqual(ShorelineIso2Catalog.KitVersion, c["kit"] as string,
                                $"'{ContractPath(style)}' is not the kit this catalog describes.");
                Assert.AreEqual(style, c["style"] as string,
                                $"'{ContractPath(style)}' claims a style that is not its folder.");
            }
        }

        [Test]
        public void EveryContract_PromisesZeroBakedWater()
        {
            // The kit's load-bearing contract, restated by the kit itself. The engine shader owns the
            // waterline; a tile that baked one would have to be lined up against a live tide contour,
            // which is exactly the problem this kit exists to not have.
            foreach (string style in Styles)
            {
                var water = MiniJson.Dict(Contract(style), "water");
                Assert.IsNotNull(water, $"'{ContractPath(style)}' has no water block.");
                Assert.AreEqual("none", water["baked"] as string,
                                $"'{ContractPath(style)}' no longer promises zero baked water.");
            }
        }

        // =====================================================================================
        // 2. literal ⟷ contract ⟷ imported asset
        // =====================================================================================

        [Test]
        public void EverySheet_ImportsAtTheSizeItsOwnContractPublishes()
        {
            var table = new StringBuilder("[shore-v8] sheet sizes vs their contracts:\n");

            foreach (string style in Styles)
            {
                var c = Contract(style);
                foreach (var kv in Grids)
                {
                    string stem = kv.Key, path = SheetPath(style, stem);

                    // leg 2: what the LIVE contract says this sheet's grid is.
                    var cell = ShorelineIso2Catalog.ContractCell(c, stem);
                    Assert.IsNotNull(cell, $"'{path}': the contract publishes no cell size.");
                    Assert.AreEqual((Cell, Cell), cell.Value, $"'{path}' cell");

                    string[] rows = ShorelineIso2Catalog.ContractRows(c, stem);
                    string[] cols = ShorelineIso2Catalog.ContractCols(c, stem);
                    // Ground publishes prose for cols (they are world positions, not named pieces);
                    // Contact is a single unnamed row. Fall back to the catalog's count for those two.
                    int nCols = cols?.Length ?? ShorelineIso2Catalog.GroundVariants;
                    int nRows = rows?.Length ?? 1;
                    var fromContract = new Vector2Int(nCols, nRows);

                    // leg 1: the README literal must agree with the live contract.
                    Assert.AreEqual(kv.Value, fromContract,
                        $"'{path}': the contract publishes a {fromContract.x}×{fromContract.y} grid " +
                        $"but the drop README says {kv.Value.x}×{kv.Value.y}. One of them drifted.");

                    // leg 3: the imported texture must match both.
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    Assert.IsNotNull(tex, $"'{path}' failed to load as a Texture2D.");

                    string mismatch = GridMismatch(tex.width, tex.height, fromContract);
                    Assert.IsNull(mismatch,
                        $"'{path}' is {mismatch}. A sheet over the importer's cap imports SILENTLY " +
                        "DOWNSCALED and the sprite COUNT still comes out right — this is the assert " +
                        "that catches it.");

                    table.AppendLine($"  {style}/{stem,-8} {tex.width,3}×{tex.height,-3} " +
                                     $"= {fromContract.x}×{fromContract.y} cells of {Cell}");
                }
            }
            Debug.Log(table.ToString());
        }

        [Test]
        public void EverySheet_SitsUnderTheImportSizeCap()
        {
            foreach (string style in Styles)
            foreach (string stem in Grids.Keys)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath(style, stem));
                Assert.IsNotNull(tex);
                Assert.LessOrEqual(Math.Max(tex.width, tex.height), ShorelineIso2Catalog.ImportSizeCap,
                    $"'{style}/{stem}' is over the {ShorelineIso2Catalog.ImportSizeCap} px cap — " +
                    "Unity has already downscaled it and the slice count would still look right.");
            }
        }

        [Test]
        public void EveryContract_PublishesTheRowsAndColumnsTheReadmeClaims()
        {
            foreach (string style in Styles)
            {
                var c = Contract(style);

                CollectionAssert.AreEqual(GroundRows, ShorelineIso2Catalog.ContractRows(c, "Ground"),
                                          $"{style}: ground rows");
                CollectionAssert.AreEqual(FringeRows, ShorelineIso2Catalog.ContractRows(c, "Fringe"),
                                          $"{style}: fringe rows");
                CollectionAssert.AreEqual(FringeCols, ShorelineIso2Catalog.ContractCols(c, "Fringe"),
                                          $"{style}: fringe columns");
                CollectionAssert.AreEqual(CliffRows, ShorelineIso2Catalog.ContractRows(c, "Cliff"),
                                          $"{style}: cliff rows");
                CollectionAssert.AreEqual(CliffCols, ShorelineIso2Catalog.ContractCols(c, "Cliff"),
                                          $"{style}: cliff columns");
                CollectionAssert.AreEqual(DuneRows, ShorelineIso2Catalog.ContractRows(c, "Dune"),
                                          $"{style}: dune rows");
                CollectionAssert.AreEqual(DuneCols, ShorelineIso2Catalog.ContractCols(c, "Dune"),
                                          $"{style}: dune columns");
                CollectionAssert.AreEqual(ContactCols, ShorelineIso2Catalog.ContractCols(c, "Contact"),
                                          $"{style}: contact columns");
            }
        }

        [Test]
        public void Catalog_RowAndColumnMaps_MatchTheContract()
        {
            var c = Contract("nat");
            CollectionAssert.AreEqual(ShorelineIso2Catalog.ContractRows(c, "Ground"),
                                      ShorelineIso2Catalog.GroundMaterials, "ground rows");
            CollectionAssert.AreEqual(ShorelineIso2Catalog.ContractRows(c, "Fringe"),
                                      ShorelineIso2Catalog.FringeMaterials, "fringe rows");
            CollectionAssert.AreEqual(ShorelineIso2Catalog.ContractCols(c, "Fringe"),
                                      ShorelineIso2Catalog.FringePieces, "fringe columns");
            CollectionAssert.AreEqual(ShorelineIso2Catalog.ContractRows(c, "Cliff"),
                                      ShorelineIso2Catalog.CliffBands, "cliff rows");
            CollectionAssert.AreEqual(ShorelineIso2Catalog.ContractCols(c, "Cliff"),
                                      ShorelineIso2Catalog.CliffPieces, "cliff columns");
            CollectionAssert.AreEqual(ShorelineIso2Catalog.ContractRows(c, "Dune"),
                                      ShorelineIso2Catalog.DuneBands, "dune rows");
            CollectionAssert.AreEqual(ShorelineIso2Catalog.ContractCols(c, "Dune"),
                                      ShorelineIso2Catalog.DunePieces, "dune columns");
            CollectionAssert.AreEqual(ShorelineIso2Catalog.ContractCols(c, "Contact"),
                                      ShorelineIso2Catalog.ContactPieces, "contact columns");

            // The dune is the cliff's nine landform pieces WITHOUT the cave — a dune has no arch.
            CollectionAssert.AreEqual(ShorelineIso2Catalog.CliffPieces.Take(9).ToArray(),
                                      ShorelineIso2Catalog.DunePieces,
                                      "dune pieces = cliff pieces minus the cave");
            CollectionAssert.DoesNotContain(ShorelineIso2Catalog.DunePieces,
                                            ShorelineIso2Catalog.CavePiece);
        }

        [Test]
        public void BothStyles_PublishIdenticalGeometry()
        {
            // The kit's central promise: a map authored against `nat` drops straight onto `gfx`. If a
            // re-bake ever changes one style's grid alone, every map built on the pair goes wrong at
            // once and nothing else would notice.
            Assert.IsTrue(ShorelineIso2Catalog.ContractsAgreeOnGeometry(out string difference),
                          $"the two styles' contracts disagree — {difference}");
        }

        // =====================================================================================
        // 3. the index arithmetic
        // =====================================================================================

        [Test]
        public void Catalog_IndexesRowMajorFromTheTopLeft()
        {
            // Spot-checks pinning both ends and an interior cell of each sheet, so an off-by-a-row
            // cannot pass.
            Assert.AreEqual(0,  ShorelineIso2Catalog.GroundIndex("grass"), "grass is the top row");
            Assert.AreEqual(3,  ShorelineIso2Catalog.GroundIndex("grass", 3), "the FOURTH ground column (v7 had three)");
            Assert.AreEqual(8,  ShorelineIso2Catalog.GroundIndex("sand"), "sand is row 2 of 4 columns");
            Assert.AreEqual(23, ShorelineIso2Catalog.GroundIndex("shelf", 3), "the last ground cell");

            Assert.AreEqual(0,  ShorelineIso2Catalog.FringeIndex("grass", "edN"));
            Assert.AreEqual(24, ShorelineIso2Catalog.FringeIndex("sand", "edN"), "sand is row 2 of 12 columns");
            Assert.AreEqual(35, ShorelineIso2Catalog.FringeIndex("sand", "inNW"), "the last fringe cell");

            Assert.AreEqual(0,  ShorelineIso2Catalog.CliffIndex("cap", "faceS"));
            Assert.AreEqual(10, ShorelineIso2Catalog.CliffIndex("mid", "faceS"), "mid is row 1 of 10 columns");
            Assert.AreEqual(29, ShorelineIso2Catalog.CliffIndex("toe", ShorelineIso2Catalog.CavePiece),
                            "the cave is the last cell of the toe row");

            Assert.AreEqual(0,  ShorelineIso2Catalog.DuneIndex("cap", "faceS"));
            Assert.AreEqual(9,  ShorelineIso2Catalog.DuneIndex("toe", "faceS"), "the toe band is row 1 of 9");
            Assert.AreEqual(17, ShorelineIso2Catalog.DuneIndex("toe", "diagSE"), "the last dune cell");

            Assert.AreEqual(0, ShorelineIso2Catalog.ContactIndex("n"));
            Assert.AreEqual(4, ShorelineIso2Catalog.ContactIndex("w"), "the last contact cell");
        }

        [Test]
        public void Catalog_RejectsAnUnknownMaterialPieceOrStyle()
        {
            // Reaching for a tile the kit doesn't bake must throw where the mistake is, not hand back
            // index 0 and paint grass where a cliff was meant.
            Assert.Throws<ArgumentOutOfRangeException>(() => ShorelineIso2Catalog.GroundIndex("foam"));
            Assert.Throws<ArgumentOutOfRangeException>(() => ShorelineIso2Catalog.CliffIndex("mid", "faceN"));
            Assert.Throws<ArgumentOutOfRangeException>(() => ShorelineIso2Catalog.DuneIndex("cap", "toe+cave"));
            Assert.Throws<ArgumentOutOfRangeException>(() => ShorelineIso2Catalog.ContactIndex("s"));
            Assert.Throws<ArgumentOutOfRangeException>(() => ShorelineIso2Catalog.SheetPath("bright", "Ground"));
            Assert.Throws<ArgumentOutOfRangeException>(() => ShorelineIso2Catalog.SheetPath("nat", "Sprites"));
        }

        [Test]
        public void Catalog_RefusesTheTwoCliffPaddingCells()
        {
            // Cells 9 and 19 are transparent. Handing back either index would paint a hole in a cliff
            // face and read as an art bug — the same trap as the road atlas's spare 48th cell.
            Assert.Throws<ArgumentOutOfRangeException>(
                () => ShorelineIso2Catalog.CliffIndex("cap", ShorelineIso2Catalog.CavePiece),
                "cap + cave is padding, not a tile");
            Assert.Throws<ArgumentOutOfRangeException>(
                () => ShorelineIso2Catalog.CliffIndex("mid", ShorelineIso2Catalog.CavePiece),
                "mid + cave is padding, not a tile");

            Assert.DoesNotThrow(
                () => ShorelineIso2Catalog.CliffIndex("toe", ShorelineIso2Catalog.CavePiece),
                "the cave DOES exist on the toe row — a sea cave is carved at the waterline");

            CollectionAssert.AreEqual(CliffPadding, ShorelineIso2Catalog.CliffPaddingIndices);
            Assert.IsTrue(ShorelineIso2Catalog.IsCliffPadding(9));
            Assert.IsTrue(ShorelineIso2Catalog.IsCliffPadding(19));
            Assert.IsFalse(ShorelineIso2Catalog.IsCliffPadding(29), "the toe cave is a real tile");
        }

        // =====================================================================================
        // 4. import settings — read back, not eyeballed
        // =====================================================================================

        [Test]
        public void EverySheet_CarriesTheLockedImportSettings()
        {
            foreach (string style in Styles)
            foreach (string stem in Grids.Keys)
            {
                string path = SheetPath(style, stem);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.IsNotNull(importer, $"'{path}' has no TextureImporter.");

                Assert.AreEqual(ArtImportPipeline.PixelsPerUnit, importer.spritePixelsPerUnit, 1e-3f,
                                $"'{path}' PPU — 32 px is 1 m and the whole kit is authored to it.");
                Assert.AreEqual(FilterMode.Point, importer.filterMode, $"'{path}' filter");
                Assert.IsFalse(importer.mipmapEnabled, $"'{path}' mips must be off");

                // ⚠ Load-bearing, not tidy: Fringe and Contact carry SOFT alpha (measured 92–235) for
                // the soil under-shadow and the AO overlays. Block compression quantises exactly that
                // gradient, and the sheet still looks broadly right — only this assert catches it.
                Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression,
                                $"'{path}' must not be lossily compressed — it carries soft AO alpha.");
                Assert.IsFalse(importer.crunchedCompression, $"'{path}' crunch");

                // This kit is all COLOUR — it ships no mask/normal/ID channel — so sRGB stays ON.
                // (The inverse trap, a data sheet left in sRGB, is what bit the tree kit.)
                var s = new TextureImporterSettings();
                importer.ReadTextureSettings(s);
                Assert.IsTrue(s.sRGBTexture,
                              $"'{path}' is colour art, not a data channel — sRGB belongs ON.");
                Assert.IsTrue(s.alphaIsTransparency, $"'{path}' alphaIsTransparency");
            }
        }

        // =====================================================================================
        // 5. once sliced, the slice matches the grid
        // =====================================================================================

        [Test]
        public void SlicedSheets_HaveOneCentredSpritePerCell()
        {
            int checkedSheets = 0;

            foreach (string style in Styles)
            foreach (var kv in Grids)
            {
                string path = SheetPath(style, kv.Key);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.IsNotNull(importer, $"'{path}' has no TextureImporter.");

                var slices = SlicesOf(path);

                // ⚠ "Multiple mode" is NOT the same question as "sliced". A freshly imported sheet
                // here comes in at spriteMode 2 with an EMPTY rect table and yields exactly one
                // sprite — so gating on the mode alone (what the v7 suite does, and what v7 never
                // exercises because its sheets ship pre-sliced) turns the un-sliced state into a
                // hard failure instead of a skip. Every sheet in this kit is at least 5 cells, so
                // "more than one sprite" is an honest test for the slice having actually run.
                if (importer.spriteImportMode != SpriteImportMode.Multiple || slices.Length <= 1)
                    continue;

                checkedSheets++;
                Assert.AreEqual(kv.Value.x * kv.Value.y, slices.Length,
                                $"'{path}' should slice to {kv.Value.x}×{kv.Value.y} cells.");

                foreach (var s in slices)
                {
                    Assert.AreEqual((float)Cell, s.rect.width,  0.01f, $"'{s.name}' cell width");
                    Assert.AreEqual((float)Cell, s.rect.height, 0.01f, $"'{s.name}' cell height");
                    Assert.IsTrue(PivotIsCellCentre(s.pivot),
                                  $"'{s.name}' pivot is {s.pivot}, not the cell centre " +
                                  $"({Cell / 2f}, {Cell / 2f}) — a tilemap places by cell, so any " +
                                  "other pivot shifts a painted tile off the cell it went into.");
                }
            }

            if (checkedSheets == 0)
                Assert.Ignore("No v8 sheet is sliced yet — run " +
                              "Hidden Harbours ▸ Art ▸ Import (after a new drop) ▸ Slice Environment + VFX Sheets in the editor. " +
                              "(The art lands in one PR; the slice is an editor step the owner runs.)");
        }

        /// <summary>
        /// The padding claim, measured against actual pixels: cells 9 and 19 of <c>Cliff.png</c> are
        /// fully transparent in both styles, and cell 29 — the toe cave — is not.
        ///
        /// <para>Verified offline across all ten delivered sheets at import (2026-07-28): the only
        /// empty cells anywhere in the kit are these two, and no sheet carries a single blue-dominant
        /// pixel. This re-checks the pair in-engine on every run.</para>
        ///
        /// <para><b>No GPU gate, deliberately.</b> The PNG is decoded from its own bytes with
        /// <c>Texture2D.LoadImage</c> and read via <c>GetPixels32</c> — the pair this suite already
        /// established as safe on CI's null graphics device (<c>CharacterRigBakeTests</c>,
        /// <c>FishingKitBakeTests</c>). Gating it on a graphics device would skip on CI exactly the
        /// check that matters, and skipping reads as green. Decoding the FILE also measures the
        /// original art rather than the imported texture, so an importer downscale cannot hide here
        /// either.</para>
        /// </summary>
        [Test]
        public void CliffPaddingCells_AreActuallyEmpty()
        {
            const int cols = 10;
            foreach (string style in Styles)
            {
                string path = SheetPath(style, "Cliff");
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
                try
                {
                    Assert.IsTrue(tex.LoadImage(File.ReadAllBytes(path), markNonReadable: false),
                                  $"'{path}' is not a decodable PNG.");
                    Color32[] px = tex.GetPixels32();

                    foreach (int index in CliffPadding)
                        Assert.AreEqual(0, MaxAlphaOfCell(tex, px, index % cols, index / cols),
                            $"'{style}/Cliff' cell {index} should be transparent padding " +
                            $"({CliffRows[index / cols]} + cave), but it carries pixels.");

                    Assert.Greater(MaxAlphaOfCell(tex, px, 9, 2), 0,
                        $"'{style}/Cliff' cell 29 is the TOE cave and must carry art — if this is " +
                        "empty the cave was dropped from the bake, not merely padded.");
                }
                finally { UnityEngine.Object.DestroyImmediate(tex); }
            }
        }

        /// <summary>Highest alpha in one 32×32 cell. <c>GetPixels32</c> is bottom-up row-major, so the
        /// contract's row 0 (the TOP row) is the highest y — the same flip every slicer here does.</summary>
        private static int MaxAlphaOfCell(Texture2D tex, Color32[] px, int col, int row)
        {
            int rows = tex.height / Cell;
            int x0 = col * Cell, y0 = (rows - 1 - row) * Cell;
            int max = 0;
            for (int y = 0; y < Cell; y++)
            {
                int rowStart = (y0 + y) * tex.width + x0;
                for (int x = 0; x < Cell; x++) max = Math.Max(max, px[rowStart + x].a);
            }
            return max;
        }

        // =====================================================================================
        // 6. THE SABOTAGES — every bar above, shown to bite
        // =====================================================================================

        /// <summary>
        /// Sabotage the size bar: a sheet one CELL ROW short is the shape a silent downscale or a
        /// drifted re-bake takes. The bar must name the sheet, and must stay quiet on the truth.
        /// </summary>
        [Test]
        public void Sabotage_ASheetOneRowShort_TripsTheSizeBar()
        {
            var grid = Grids["Cliff"];                       // 10×3 → 320×96
            Assert.IsNull(GridMismatch(320, 96, grid), "the true size must pass");

            var table = new StringBuilder("[shore-v8] SABOTAGE — size bar:\n");
            foreach (var (w, h, what) in new[]
                     {
                         (320, 64,  "one cell row short (a dropped band)"),
                         (288, 96,  "one cell column short (a dropped piece)"),
                         (160, 48,  "half size (the classic silent 2048 downscale)"),
                         (321, 96,  "one PIXEL wide (an off-by-one crop)"),
                     })
            {
                string hit = GridMismatch(w, h, grid);
                table.AppendLine($"  {w,3}×{h,-3} {what,-46} → {(hit == null ? "MISSED" : "caught")}");
                Assert.IsNotNull(hit, $"SABOTAGE NOT DETECTED — {w}×{h} ({what}) passed the size bar.");
            }
            Debug.Log(table.ToString());
        }

        /// <summary>
        /// Sabotage the pivot bar with a ONE PIXEL offset — the smallest error that still shifts a
        /// painted tile, and the one an eyeball never catches on a 32 px cell.
        /// </summary>
        [Test]
        public void Sabotage_APivotOnePixelOff_TripsThePivotBar()
        {
            Assert.IsTrue(PivotIsCellCentre(new Vector2(16f, 16f)), "the true pivot must pass");

            var table = new StringBuilder("[shore-v8] SABOTAGE — pivot bar (cell centre is 16,16):\n");
            foreach (var (p, what) in new[]
                     {
                         (new Vector2(16f, 15f), "1 px down — a tile row off, per band stacked"),
                         (new Vector2(15f, 16f), "1 px left"),
                         (new Vector2(16f, 0f),  "bottom-centre — the obvious wrong default"),
                         (new Vector2(0f, 32f),  "top-left — the WHARF kit's pivot, wrong here"),
                     })
            {
                bool passed = PivotIsCellCentre(p);
                table.AppendLine($"  ({p.x,4:F0},{p.y,4:F0}) {what,-48} → {(passed ? "MISSED" : "caught")}");
                Assert.IsFalse(passed, $"SABOTAGE NOT DETECTED — pivot {p} ({what}) passed.");
            }
            Debug.Log(table.ToString());
        }

        /// <summary>
        /// Sabotage the cross-style geometry check by mutating ONE style's contract in memory. This is
        /// the failure mode with no other witness: both styles import fine, both slice fine, and every
        /// map authored on the pair is quietly wrong.
        /// </summary>
        [Test]
        public void Sabotage_AStyleWhoseGridDrifted_TripsTheCrossStyleCheck()
        {
            var a = Contract("nat");
            var b = Contract("gfx");
            Assert.IsTrue(ShorelineIso2Catalog.ContractsAgreeOnGeometry(a, b, out _),
                          "the two shipped contracts must agree before we corrupt one");

            // (i) a dropped column — gfx re-baked without the cave.
            var droppedCol = Contract("gfx");
            var cliff = SheetNodeOf(droppedCol, "Cliff");
            var cols = cliff["cols"] as List<object>;
            cols.RemoveAt(cols.Count - 1);
            Assert.IsFalse(ShorelineIso2Catalog.ContractsAgreeOnGeometry(a, droppedCol, out string d1),
                           "SABOTAGE NOT DETECTED — a style missing a cliff column passed.");

            // (ii) two pieces swapped — the same COUNT, so a count-only check would sail past.
            var swapped = Contract("gfx");
            var fringe = SheetNodeOf(swapped, "Fringe");
            var fcols = fringe["cols"] as List<object>;
            (fcols[0], fcols[1]) = (fcols[1], fcols[0]);
            Assert.IsFalse(ShorelineIso2Catalog.ContractsAgreeOnGeometry(a, swapped, out string d2),
                           "SABOTAGE NOT DETECTED — two swapped fringe columns passed. A check that " +
                           "only counted would have missed this, which is the point of comparing order.");

            // (iii) a changed cell size — the shape a PPU or grid change takes.
            var recelled = Contract("gfx");
            var dune = SheetNodeOf(recelled, "Dune");
            dune["cell"] = new List<object> { 32.0, 48.0 };
            Assert.IsFalse(ShorelineIso2Catalog.ContractsAgreeOnGeometry(a, recelled, out string d3),
                           "SABOTAGE NOT DETECTED — a style with a different dune cell passed.");

            Debug.Log("[shore-v8] SABOTAGE — cross-style geometry:\n" +
                      $"  dropped cliff column  → caught: {d1}\n" +
                      $"  swapped fringe cols   → caught: {d2}\n" +
                      $"  re-celled dune        → caught: {d3}");
        }

        /// <summary>
        /// Sabotage the padding guard: prove <see cref="ShorelineIso2Catalog.CliffIndex"/> refusing
        /// cap/mid+cave is what stands between a painter and two empty cells. The naive row-major
        /// arithmetic every other sheet uses would hand back 9 and 19 without complaint.
        /// </summary>
        [Test]
        public void Sabotage_NaiveRowMajor_WouldHandBackTheEmptyCliffCells()
        {
            int naiveCap = Array.IndexOf(CliffRows, "cap") * CliffCols.Length
                         + Array.IndexOf(CliffCols, "toe+cave");
            int naiveMid = Array.IndexOf(CliffRows, "mid") * CliffCols.Length
                         + Array.IndexOf(CliffCols, "toe+cave");

            Assert.AreEqual(9,  naiveCap, "the naive arithmetic really does resolve cap+cave to 9");
            Assert.AreEqual(19, naiveMid, "…and mid+cave to 19");
            CollectionAssert.AreEqual(new[] { naiveCap, naiveMid },
                                      ShorelineIso2Catalog.CliffPaddingIndices,
                                      "the guarded indices ARE the two the naive arithmetic returns");

            Debug.Log($"[shore-v8] SABOTAGE — padding guard: naive row-major returns {naiveCap} and " +
                      $"{naiveMid} for cap/mid + cave; both are transparent cells. CliffIndex throws " +
                      "on both, so a painter gets an exception at the mistake instead of a hole in a " +
                      "cliff face.");
        }

        private static Dictionary<string, object> SheetNodeOf(Dictionary<string, object> contract,
                                                              string stem)
        {
            var sheets = MiniJson.Dict(contract, "sheets");
            Assert.IsNotNull(sheets, "contract has no sheets block");
            return sheets[$"{stem}.png"] as Dictionary<string, object>;
        }
    }
}
