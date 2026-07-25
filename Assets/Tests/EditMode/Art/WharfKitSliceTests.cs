using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Guards the import of the WHARF / DOCK tile kit under <c>Art/Tilesets/Wharf/</c> — the working
    /// waterfront's deck, its fittings, and the breakwater arms.
    ///
    /// <para>Every dimension, row map, column map and pivot below is restated as a LITERAL from the kit
    /// README and its three sidecars as delivered — deliberately NOT imported from
    /// <see cref="WharfKitCatalog"/> and not read back out of the live JSON. Asserting the catalog
    /// against the catalog is the self-referential blind spot that let mirrored boat art ship; these
    /// literals pin the contract from outside, so a drifted re-bake, an edited sidecar and a mistyped
    /// catalog each fail loudly and each failure points at its own file.</para>
    ///
    /// <para><b>The load-bearing fact this file exists to protect is the 32×56 CELL.</b> The deck is the
    /// top 32 rows; the bottom 24 are the vertical face and waterline foam that overhang DOWNWARD over
    /// the cell below. That is why the atlas pivots top-LEFT where every other tile sheet in the repo
    /// pivots centre. A centre pivot here would sink every wharf tile 12 px into the water, and it would
    /// do it consistently enough to look like an art bug rather than an import one.</para>
    ///
    /// <para>Nothing here touches the GPU, so no Null-device gate is needed. Slice-state checks are
    /// skipped (not failed) while a sheet is still Single-mode — the art lands in one PR and the slice is
    /// an editor menu the owner runs, so failing on "not sliced yet" would redden main for a step that is
    /// by design manual.</para>
    /// </summary>
    public class WharfKitSliceTests
    {
        private const string Wharf = "Assets/_Project/Art/Tilesets/Wharf/";
        private const string AtlasPath = Wharf + "WharfAtlas.png";
        private const string BreakwatersPath = Wharf + "WharfBreakwaters.png";
        private const string OverlaysPath = Wharf + "WharfOverlays.png";
        private const string OverlaysSidecar = Wharf + "WharfOverlays.json";
        private const string RigPath = "docs/art/rigs/wharfKitRig.js";

        // ---- the kit contract, restated as literals ------------------------------------------

        // Deck: 32 px of tile + 24 px of face/foam that hangs below it.
        private const int DeckW = 32, CellH = 56, DeckH = 32, FaceH = 24;
        private const int AtlasCols = 17, AtlasRows = 7;

        // Breakwater armour: 3 variants across, 4 types down.
        private const int ArmourW = 48, ArmourH = 60;
        private const int ArmourCols = 3, ArmourRows = 4;

        private const int OverlaySheetW = 520, OverlaySheetH = 41;

        private static readonly string[] DeckMaterials = { "quay", "lowpier", "tallpier", "float" };

        private static readonly string[] DeckVariants =
        {
            "ctr",
            "edN", "edE", "edS", "edW",
            "coNE", "coSE", "coSW", "coNW",
            "capN", "capS", "capE", "capW",
            "diNE", "diSE", "diSW", "diNW",
        };

        private static readonly string[] ArmourTypes = { "riprap", "crib", "wall", "sheet" };
        private static readonly string[] ArmourVariants = { "straight", "diag", "end" };

        /// <summary>
        /// The 14 fittings verbatim from <c>WharfOverlays.json</c> — name, rect (x,y,w,h with y from the
        /// sheet's TOP) and pivot (px,py from the ITEM's top-left), in the sheet's pack order.
        /// </summary>
        private static readonly (string name, int x, int y, int w, int h, int px, int py)[] Fittings =
        {
            ("rail_wood_h",   0, 0, 32, 14, 0, 13),
            ("rail_wood_v",  33, 0,  6, 32, 1,  0),
            ("rail_wood_d",  40, 0, 34, 20, 0,  2),
            ("rail_pipe_h",  75, 0, 32, 14, 0, 13),
            ("rail_pipe_v", 108, 0,  6, 32, 1,  0),
            ("rail_pipe_d", 115, 0, 34, 20, 0,  2),
            ("cleat",       150, 0, 11,  7, 5,  4),
            ("bollard",     162, 0, 11, 12, 5, 11),
            ("ring",        174, 0,  9,  7, 4,  3),
            ("dolphin",     184, 0, 15, 20, 7, 19),
            ("ladder",      200, 0,  9, 20, 4,  0),
            ("tyre",        210, 0, 11, 14, 5,  0),
            ("pilehead",    222, 0,  7, 16, 3, 15),
            ("gangway",     230, 0, 16, 40, 8,  0),
        };

        private static string RepoPath(string relative) =>
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, relative);

        private static Sprite[] SlicesOf(string assetPath) =>
            AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Sprite>().ToArray();

        // ---- the art is on disk at the size the contract claims -------------------------------

        [Test]
        public void EveryKitFile_IsOnDisk()
        {
            foreach (var p in new[] { AtlasPath, BreakwatersPath, OverlaysPath, OverlaysSidecar,
                                      Wharf + "WharfAtlas.json", Wharf + "WharfBreakwaters.json" })
                Assert.IsTrue(File.Exists(p), $"'{p}' is missing — the wharf kit import is incomplete.");

            // The rig is the bake source of truth (ADR 0021): the deck PNG is one bake of it.
            Assert.IsTrue(File.Exists(RepoPath(RigPath)), $"'{RigPath}' is missing.");
        }

        [Test]
        public void Atlas_IsSeventeenVariantsByFourMaterialsAndFourBobFrames()
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
            Assert.IsNotNull(tex, "WharfAtlas.png failed to load as a Texture2D.");
            Assert.AreEqual(AtlasCols * DeckW, tex.width, "atlas width = 17 variants × 32 px");
            Assert.AreEqual(AtlasRows * CellH, tex.height,
                            "atlas height = 7 rows × the 56 px CELL (not the 32 px deck)");
        }

        [Test]
        public void Breakwaters_AreThreeVariantsByFourArmourTypes()
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(BreakwatersPath);
            Assert.IsNotNull(tex, "WharfBreakwaters.png failed to load as a Texture2D.");
            Assert.AreEqual(ArmourCols * ArmourW, tex.width);
            Assert.AreEqual(ArmourRows * ArmourH, tex.height);
        }

        [Test]
        public void Overlays_AreTheSizeTheSidecarAssumes()
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(OverlaysPath);
            Assert.IsNotNull(tex, "WharfOverlays.png failed to load as a Texture2D.");
            Assert.AreEqual(OverlaySheetW, tex.width);
            Assert.AreEqual(OverlaySheetH, tex.height);
        }

        [Test]
        public void TheCell_IsDeckPlusFace()
        {
            // Stated as its own test because it is the kit's whole placement contract, and because a
            // future re-bake that "tidied" the cell to 32×32 would silently delete every deck face.
            Assert.AreEqual(CellH, DeckH + FaceH, "the fixture's own literals must agree");
            Assert.AreEqual(DeckH, WharfKitCatalog.DeckHeight, "deck rows");
            Assert.AreEqual(FaceH, WharfKitCatalog.FaceHeight, "overhanging face rows");
            Assert.AreEqual(CellH, WharfKitCatalog.DeckCellHeight, "full cell");
            Assert.AreEqual(DeckW, WharfKitCatalog.DeckTileWidth, "tile width = 1 m at the locked PPU");
        }

        // ---- the live sidecar still says what the literals say ---------------------------------

        [Test]
        public void OverlaySidecar_MatchesTheKitContractLiterals()
        {
            var parsed = WharfOverlaySlicer.ParseSidecar(File.ReadAllText(OverlaysSidecar));
            Assert.AreEqual(Fittings.Length, parsed.Count, "fitting count");

            for (int i = 0; i < Fittings.Length; i++)
            {
                var want = Fittings[i];
                var got = parsed[i];
                Assert.AreEqual(want.name, got.name, $"fitting {i} name (parse order is by pack position)");
                Assert.AreEqual(want.x, got.x, $"'{want.name}' x");
                Assert.AreEqual(want.y, got.y, $"'{want.name}' y");
                Assert.AreEqual(want.w, got.w, $"'{want.name}' w");
                Assert.AreEqual(want.h, got.h, $"'{want.name}' h");
                Assert.AreEqual(want.px, got.pivotX, $"'{want.name}' pivotX");
                Assert.AreEqual(want.py, got.pivotY, $"'{want.name}' pivotY");
            }
        }

        [Test]
        public void OverlaySidecar_ParseIsOrderedByPackPosition_NotByJsonKeyOrder()
        {
            // MiniJson returns a plain Dictionary, whose iteration order is not contractual. Slice order
            // is what lands in the .meta, so it must not depend on it. Feeding the SAME frames in a
            // shuffled JSON key order must produce the same list.
            const string shuffled = @"{ ""frames"": {
                ""gangway"":     { ""x"": 230, ""y"": 0, ""w"": 16, ""h"": 40, ""pivotX"": 8, ""pivotY"": 0 },
                ""cleat"":       { ""x"": 150, ""y"": 0, ""w"": 11, ""h"":  7, ""pivotX"": 5, ""pivotY"": 4 },
                ""rail_wood_h"": { ""x"":   0, ""y"": 0, ""w"": 32, ""h"": 14, ""pivotX"": 0, ""pivotY"": 13 }
            } }";

            var parsed = WharfOverlaySlicer.ParseSidecar(shuffled);
            CollectionAssert.AreEqual(new[] { "rail_wood_h", "cleat", "gangway" },
                                      parsed.Select(p => p.name).ToArray(),
                                      "must sort by x, not by the order the JSON listed the keys");
        }

        [Test]
        public void OverlaySidecar_AFrameMissingAFieldStopsTheSlice()
        {
            // A half-read sidecar must not slice: a fitting with no width becomes a zero-size sprite,
            // which reads as art corruption rather than as bad data.
            const string missingW = @"{ ""frames"": {
                ""cleat"": { ""x"": 150, ""y"": 0, ""h"": 7, ""pivotX"": 5, ""pivotY"": 4 } } }";
            Assert.Throws<FormatException>(() => WharfOverlaySlicer.ParseSidecar(missingW));

            const string noFrames = @"{ ""sheet"": ""nothing here"" }";
            Assert.Throws<FormatException>(() => WharfOverlaySlicer.ParseSidecar(noFrames));
        }

        // ---- the top-left → bottom-left flip ---------------------------------------------------

        [Test]
        public void FittingRects_FlipFromTheSidecarsTopLeftSpaceIntoUnitys()
        {
            var items = Fittings.Select(f => new WharfOverlaySlicer.SidecarItem
            {
                name = f.name, x = f.x, y = f.y, w = f.w, h = f.h, pivotX = f.px, pivotY = f.py,
            }).ToList();

            var rects = WharfOverlaySlicer.BuildRects(items, OverlaySheetH);
            Assert.AreEqual(Fittings.Length, rects.Length);

            for (int i = 0; i < Fittings.Length; i++)
            {
                var want = Fittings[i];
                var got = rects[i];

                Assert.AreEqual($"WharfOverlays_{want.name}", got.name, $"fitting {i} slice name");
                Assert.AreEqual((float)want.x, got.rect.x, 1e-4f, $"'{want.name}' rect x is unchanged");
                Assert.AreEqual((float)(OverlaySheetH - (want.y + want.h)), got.rect.y, 1e-4f,
                                $"'{want.name}' rect y must flip to bottom-origin");
                Assert.AreEqual((float)want.w, got.rect.width, 1e-4f, $"'{want.name}' width");
                Assert.AreEqual((float)want.h, got.rect.height, 1e-4f, $"'{want.name}' height");

                Assert.AreEqual(want.px / (float)want.w, got.pivot.x, 1e-5f, $"'{want.name}' pivot x");
                Assert.AreEqual((want.h - want.py) / (float)want.h, got.pivot.y, 1e-5f,
                                $"'{want.name}' pivot y must flip and normalize");
            }
        }

        [Test]
        public void FittingPivots_MeanDifferentThingsPerFitting_AndEachRuleHolds()
        {
            // This is the reason the overlays are sidecar-sliced instead of grid-sliced. The kit's own
            // note lists four pivot KINDS; measuring them shows the grouping is finer than that note
            // suggests, so the rules asserted here are the ones the pixels actually follow.
            var byName = Fittings.ToDictionary(f => f.name);
            int PxAboveBase(string n) => byName[n].h - byName[n].py;
            float PivotY(string n) => PxAboveBase(n) / (float)byName[n].h;

            // Things that STAND on the deck (or in the water) pivot exactly ONE PIXEL above their base —
            // the same contact rule the shoreline kit's sea stacks use, so a bollard and a boulder plant
            // by one convention.
            foreach (var n in new[] { "bollard", "dolphin", "pilehead" })
                Assert.AreEqual(1, PxAboveBase(n), $"'{n}' stands: pivot must sit on its base row");

            // Things that HANG off a deck face pivot exactly at the TOP, so the sprite falls away from
            // where it attaches.
            foreach (var n in new[] { "ladder", "tyre", "gangway" })
                Assert.AreEqual(1f, PivotY(n), 1e-5f, $"'{n}' hangs: pivot must be at its top edge");

            // The cleat and the recessed ring are LOW, FLAT fittings — a few pixels tall. At a ¾ camera
            // their deck contact point projects into the MIDDLE of the sprite, not onto its bottom edge,
            // so neither follows the stander rule and neither is a bug. (The cleat reads as a "base"
            // pivot in the kit's note and as 0.43 in the pixels; both are true of a 7 px-tall object.)
            foreach (var n in new[] { "cleat", "ring" })
                Assert.That(PivotY(n), Is.InRange(0.35f, 0.65f),
                            $"'{n}' is a flat deck fitting: its contact point projects mid-sprite");

            // Rails pivot on the EDGE LINE they run along, and which edge that is decides which end of
            // the sprite the line falls on: a horizontal run's line is its bottom row, a vertical run's
            // is its top.
            foreach (var n in new[] { "rail_wood_h", "rail_pipe_h" })
                Assert.AreEqual(1, PxAboveBase(n), $"'{n}' runs along an N/S edge: line at its base");
            foreach (var n in new[] { "rail_wood_v", "rail_pipe_v" })
                Assert.AreEqual(1f, PivotY(n), 1e-5f, $"'{n}' runs along an E/W edge: line at its top");

            // Rails must TILE the edge they run along, or a long quay grows gaps every metre.
            Assert.AreEqual(DeckW, byName["rail_wood_h"].w, "a horizontal rail must be exactly one tile wide");
            Assert.AreEqual(DeckW, byName["rail_pipe_h"].w, "a horizontal rail must be exactly one tile wide");
            Assert.AreEqual(DeckW, byName["rail_wood_v"].h, "a vertical rail must be exactly one tile tall");
            Assert.AreEqual(DeckW, byName["rail_pipe_v"].h, "a vertical rail must be exactly one tile tall");
        }

        [Test]
        public void WoodAndPipeRails_AreGeometricallyIdentical_SoTheyAreDropInSwaps()
        {
            // Same rect and same pivot for each run — so choosing wood or galvanised pipe for a stretch
            // of deck is a paint decision, never a placement one. (The punt's two outboard paint builds
            // share their cell for the same reason.) A re-bake that drifted one and not the other would
            // shift every rail of that material by the difference, which is exactly the kind of one-pixel
            // creep nobody spots by eye.
            var byName = Fittings.ToDictionary(f => f.name);
            foreach (var run in new[] { "h", "v", "d" })
            {
                var wood = byName[$"rail_wood_{run}"];
                var pipe = byName[$"rail_pipe_{run}"];
                Assert.AreEqual(wood.w, pipe.w, $"rail '{run}' width");
                Assert.AreEqual(wood.h, pipe.h, $"rail '{run}' height");
                Assert.AreEqual(wood.px, pipe.px, $"rail '{run}' pivot x");
                Assert.AreEqual(wood.py, pipe.py, $"rail '{run}' pivot y");
            }
        }

        // ---- the catalog resolves the contract --------------------------------------------------

        [Test]
        public void Catalog_RowAndColumnMaps_MatchTheContract()
        {
            CollectionAssert.AreEqual(DeckMaterials, WharfKitCatalog.DeckMaterials, "deck materials");
            CollectionAssert.AreEqual(DeckVariants, WharfKitCatalog.DeckVariants, "deck variants");
            CollectionAssert.AreEqual(ArmourTypes, WharfKitCatalog.ArmourTypes, "armour types");
            CollectionAssert.AreEqual(ArmourVariants, WharfKitCatalog.ArmourVariants, "armour variants");
            CollectionAssert.AreEqual(Fittings.Select(f => f.name).ToArray(), WharfKitCatalog.Fittings,
                                      "fittings");

            Assert.AreEqual(AtlasCols, WharfKitCatalog.AtlasCols);
            Assert.AreEqual(AtlasRows, WharfKitCatalog.AtlasRows,
                            "3 fixed materials + the float's 4 bob frames = 7 rows");
        }

        [Test]
        public void Catalog_FloatOccupiesFourRows_AndTheFixedDecksOccupyOne()
        {
            // The kit's one animation. Getting this wrong doesn't crash — it makes a concrete quay
            // shimmer or a floating dock sit dead still, either of which reads as a shader bug.
            Assert.AreEqual(0, WharfKitCatalog.AtlasRow("quay"));
            Assert.AreEqual(1, WharfKitCatalog.AtlasRow("lowpier"));
            Assert.AreEqual(2, WharfKitCatalog.AtlasRow("tallpier"));

            for (int f = 0; f < 4; f++)
                Assert.AreEqual(3 + f, WharfKitCatalog.AtlasRow("float", f), $"float bob frame {f}");

            Assert.Throws<ArgumentOutOfRangeException>(() => WharfKitCatalog.AtlasRow("float", 4),
                "there are only 4 bob frames");
            Assert.Throws<ArgumentOutOfRangeException>(() => WharfKitCatalog.AtlasRow("quay", 1),
                "a concrete quay does not bob — asking for a frame of one is a caller bug");
        }

        [Test]
        public void Catalog_IndexesRowMajorFromTheTopLeft()
        {
            Assert.AreEqual(0, WharfKitCatalog.AtlasIndex("quay", "ctr"), "the first cell");
            Assert.AreEqual(3, WharfKitCatalog.AtlasIndex("quay", "edS"),
                            "the south edge — the one that carries the tall face");
            Assert.AreEqual(17, WharfKitCatalog.AtlasIndex("lowpier", "ctr"), "row 1 of 17 columns");
            Assert.AreEqual(34, WharfKitCatalog.AtlasIndex("tallpier", "ctr"), "row 2");
            Assert.AreEqual(51, WharfKitCatalog.AtlasIndex("float", "ctr"), "float, bob frame 0");
            Assert.AreEqual(118, WharfKitCatalog.AtlasIndex("float", "diNW", 3), "the very last cell");

            Assert.AreEqual(0, WharfKitCatalog.BreakwaterIndex("riprap", "straight"));
            Assert.AreEqual(4, WharfKitCatalog.BreakwaterIndex("crib", "diag"), "row 1 of 3 columns");
            Assert.AreEqual(11, WharfKitCatalog.BreakwaterIndex("sheet", "end"), "the last armour cell");
        }

        [Test]
        public void Catalog_RejectsAnUnknownMaterialTypeOrFitting()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => WharfKitCatalog.AtlasIndex("pontoon", "ctr"));
            Assert.Throws<ArgumentOutOfRangeException>(() => WharfKitCatalog.AtlasIndex("quay", "edNE"));
            Assert.Throws<ArgumentOutOfRangeException>(() => WharfKitCatalog.BreakwaterIndex("gabion", "end"));
            Assert.Throws<ArgumentOutOfRangeException>(() => WharfKitCatalog.FittingSlice("capstan"));
        }

        // ---- once sliced, the slice must match --------------------------------------------------

        [Test]
        public void SlicedAtlas_HasOneTallCellPerVariant_PivotedTopLeft()
        {
            var importer = AssetImporter.GetAtPath(AtlasPath) as TextureImporter;
            Assert.IsNotNull(importer, "WharfAtlas.png has no TextureImporter.");
            if (importer.spriteImportMode != SpriteImportMode.Multiple)
                Assert.Ignore("Not sliced yet — run Hidden Harbours ▸ Art ▸ Slice Environment + VFX Sheets.");

            var slices = SlicesOf(AtlasPath);
            Assert.AreEqual(AtlasCols * AtlasRows, slices.Length, "119 cells");

            foreach (var s in slices)
            {
                Assert.AreEqual((float)DeckW, s.rect.width, 0.01f, $"'{s.name}' width");
                Assert.AreEqual((float)CellH, s.rect.height, 0.01f,
                                $"'{s.name}' must be the FULL 56 px cell, not the 32 px deck");
                Assert.AreEqual(0f, s.pivot.x, 0.01f, $"'{s.name}' pivot x (tile screen origin)");
                Assert.AreEqual((float)CellH, s.pivot.y, 0.01f,
                                $"'{s.name}' must pivot at the cell TOP so the face hangs below its tile");
            }
        }

        [Test]
        public void SlicedBreakwaters_PivotOnTheCrest()
        {
            var importer = AssetImporter.GetAtPath(BreakwatersPath) as TextureImporter;
            Assert.IsNotNull(importer, "WharfBreakwaters.png has no TextureImporter.");
            if (importer.spriteImportMode != SpriteImportMode.Multiple)
                Assert.Ignore("Not sliced yet — run Hidden Harbours ▸ Art ▸ Slice Environment + VFX Sheets.");

            var slices = SlicesOf(BreakwatersPath);
            Assert.AreEqual(ArmourCols * ArmourRows, slices.Length, "12 armour pieces");

            foreach (var s in slices)
            {
                Assert.AreEqual((float)ArmourW, s.rect.width, 0.01f, $"'{s.name}' width");
                Assert.AreEqual((float)ArmourH, s.rect.height, 0.01f, $"'{s.name}' height");
                // Crest, not base: consecutive pieces butt along the crest line, and the armour types
                // have different base heights (there is a foam fringe below each).
                Assert.AreEqual(ArmourW / 2f, s.pivot.x, 0.01f, $"'{s.name}' pivot x");
                Assert.AreEqual((float)ArmourH, s.pivot.y, 0.01f, $"'{s.name}' must pivot at the CREST");
            }
        }

        [Test]
        public void SlicedOverlays_CarryTheSidecarsNamesAndPivots()
        {
            var importer = AssetImporter.GetAtPath(OverlaysPath) as TextureImporter;
            Assert.IsNotNull(importer, "WharfOverlays.png has no TextureImporter.");
            if (importer.spriteImportMode != SpriteImportMode.Multiple)
                Assert.Ignore("Not sliced yet — run Hidden Harbours ▸ Art ▸ Slice Wharf Overlay Sprites.");

            var byName = SlicesOf(OverlaysPath).ToDictionary(s => s.name);
            Assert.AreEqual(Fittings.Length, byName.Count, "fitting count");

            foreach (var f in Fittings)
            {
                string name = $"WharfOverlays_{f.name}";
                Assert.IsTrue(byName.TryGetValue(name, out var s), $"'{name}' is missing from the slice.");
                Assert.AreEqual((float)f.w, s.rect.width, 0.01f, $"'{name}' width");
                Assert.AreEqual((float)f.h, s.rect.height, 0.01f, $"'{name}' height");
                Assert.AreEqual(f.px, s.pivot.x, 0.01f, $"'{name}' pivot x (px from the rect's left)");
                Assert.AreEqual(f.h - f.py, s.pivot.y, 0.01f, $"'{name}' pivot y (px up from the base)");
            }
        }
    }
}
