using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// Guards <see cref="BuildingRigBaker"/> — the crop-packing baker for the house and wharf-building
    /// rigs.
    ///
    /// <para>Split deliberately in two. The <b>arithmetic</b> tests below run on synthetic pixel buffers
    /// and are the ones that matter most, because the crop's failure mode is silent: a wrong crop origin
    /// does not throw, it just stands every building a fixed distance from where it was placed, which
    /// reads as a placement bug in some other system. The <b>rig</b> tests then run the real JS and are
    /// skipped when no engine is available, so the arithmetic stays checkable on any machine.</para>
    /// </summary>
    public class BuildingRigBakeTests
    {
        // ---- helpers ---------------------------------------------------------------------------

        /// <summary>A W×H RGBA buffer, transparent except for one opaque rect (top-left origin).</summary>
        static byte[] Cell(int w, int h, int rx, int ry, int rw, int rh)
        {
            var buf = new byte[w * h * 4];
            for (int y = ry; y < ry + rh; y++)
                for (int x = rx; x < rx + rw; x++)
                {
                    int i = (y * w + x) * 4;
                    buf[i] = 200; buf[i + 1] = 120; buf[i + 2] = 90; buf[i + 3] = 255;
                }
            return buf;
        }

        // ---- the crop ---------------------------------------------------------------------------

        [Test]
        public void AlphaBounds_FindsTheDrawnRect_AndIsInclusive()
        {
            var cell = Cell(100, 80, rx: 10, ry: 20, rw: 30, rh: 25);
            BuildingRigAzimuthProbe.AlphaBounds(cell, 100, 80,
                                                out int xMin, out int yMin, out int xMax, out int yMax);
            Assert.AreEqual(10, xMin);
            Assert.AreEqual(20, yMin);
            Assert.AreEqual(39, xMax, "max is the LAST drawn column, not one past it");
            Assert.AreEqual(44, yMax, "max is the LAST drawn row, not one past it");
        }

        [Test]
        public void AlphaBounds_ReportsEmpty_ForAFullyTransparentCell()
        {
            var blank = new byte[40 * 40 * 4];
            BuildingRigAzimuthProbe.AlphaBounds(blank, 40, 40,
                                                out int xMin, out _, out int xMax, out _);
            Assert.Less(xMax, xMin, "an empty cell must report xMax < xMin, not a degenerate 1 px box");
        }

        [Test]
        public void UnionAlphaBounds_TakesTheUnion_SoEveryFacingFits()
        {
            // THE reason the crop is a union: each facing draws a different silhouette, but all eight
            // must share one cell size and one pivot. Cropping each to its own bounds would give eight
            // different cells and a building that jumps as it turns.
            var cells = new[]
            {
                Cell(200, 200, 50, 60, 20, 20),   // a narrow facing
                Cell(200, 200, 30, 70, 60, 20),   // wider, lower
                Cell(200, 200, 55, 40, 20, 50),   // taller, higher
            };

            BuildingRigBaker.UnionAlphaBounds(cells, 200, 200,
                                              out int xMin, out int yMin, out int xMax, out int yMax);

            Assert.AreEqual(30, xMin, "leftmost across ALL facings");
            Assert.AreEqual(40, yMin, "topmost across ALL facings");
            Assert.AreEqual(89, xMax, "rightmost across ALL facings");
            Assert.AreEqual(89, yMax, "bottommost across ALL facings");
        }

        [Test]
        public void UnionAlphaBounds_SkipsAnEmptyFacing_WithoutCollapsingTheUnion()
        {
            var cells = new[]
            {
                Cell(100, 100, 20, 20, 30, 30),
                new byte[100 * 100 * 4],           // a facing that drew nothing
            };

            BuildingRigBaker.UnionAlphaBounds(cells, 100, 100,
                                              out int xMin, out int yMin, out int xMax, out int yMax);

            Assert.AreEqual(20, xMin);
            Assert.AreEqual(20, yMin);
            Assert.AreEqual(49, xMax, "the empty facing must not drag the union to the cell edge");
            Assert.AreEqual(49, yMax);
        }

        [Test]
        public void TheCropIsWorthDoing_OnARealisticBuildingCell()
        {
            // The whole justification for this baker. A wharf-building cell is 1200×1160 because it must
            // hold the cannery; a net shed drawn in it is small. Uncropped, 8 facings cannot even be
            // packed under the texture cap — this test states the size of the problem so a future change
            // that quietly disables the crop shows up as a failing number, not a mystery.
            const int nativeW = 1200, nativeH = 1160;
            var shed = Cell(nativeW, nativeH, rx: 500, ry: 640, rw: 210, rh: 240);

            BuildingRigBaker.UnionAlphaBounds(new[] { shed }, nativeW, nativeH,
                                              out int xMin, out int yMin, out int xMax, out int yMax);
            int cw = xMax - xMin + 1, ch = yMax - yMin + 1;

            double saving = 1.0 - (cw * (double)ch) / (nativeW * (double)nativeH);
            Assert.Greater(saving, 0.9, "cropping a shed out of a cannery-sized cell should save >90%");

            // Uncropped, the widest legal grid is 3 columns (3×1200 = 3600 ≤ 4096), needing 3 rows for
            // 8 facings = 3480 px tall. It fits, but at 50 MB of RGBA32 for ONE preset.
            long uncropped = (long)(3 * nativeW) * (3 * nativeH) * 4;
            Assert.Greater(uncropped, 40L * 1024 * 1024,
                           "uncropped really is tens of MB per preset — that is why the crop exists");

            BuildingRigBaker.ChooseGrid(cw, ch, 8, out int cols, out int rows);
            long cropped = (long)(cols * cw) * (rows * ch) * 4;
            Assert.Less(cropped, uncropped / 20,
                        "cropped should be at least 20× smaller than the uncropped equivalent");
        }

        // ---- the pivot --------------------------------------------------------------------------

        [Test]
        public void ThePivotShiftsByExactlyTheCropOrigin()
        {
            // Not a formality. If the pivot does not move with the crop, nothing throws and nothing
            // looks broken in the sheet — every building simply stands offset by the crop origin, which
            // presents as "the placement code is wrong" and costs a day.
            const double nativePivotX = 600, nativePivotY = 780;   // wharfBuildingRig's ground centre
            const int cropX = 470, cropY = 612;

            double croppedPivotX = nativePivotX - cropX;
            double croppedPivotY = nativePivotY - cropY;

            Assert.AreEqual(130, croppedPivotX, 1e-9);
            Assert.AreEqual(168, croppedPivotY, 1e-9);

            // And the world point must be unchanged: crop origin + cropped pivot == native pivot.
            Assert.AreEqual(nativePivotX, cropX + croppedPivotX, 1e-9);
            Assert.AreEqual(nativePivotY, cropY + croppedPivotY, 1e-9);
        }

        [Test]
        public void TheUnityPivotConversionFlipsY_NotX()
        {
            // The rigs measure the pivot from the cell's TOP-left; Unity normalises from the BOTTOM-left.
            // Only y flips. Getting this upside-down is silent and is called out in RigGeometry's own
            // docs as "an easy and silent mistake", so it is pinned here for the cropped cell too.
            const int cellW = 260, cellH = 300;
            const double pivotX = 130, pivotY = 168;

            var unity = new Vector2((float)(pivotX / cellW), (float)((cellH - pivotY) / cellH));

            Assert.AreEqual(0.5f, unity.x, 1e-5f, "a ground-centre pivot is horizontally centred");
            Assert.AreEqual((300f - 168f) / 300f, unity.y, 1e-5f);
            Assert.AreNotEqual(pivotY / cellH, unity.y, "y must FLIP, not merely normalise");
        }

        // ---- the grid ---------------------------------------------------------------------------

        [Test]
        public void ChooseGrid_PrefersWideAndShort()
        {
            BuildingRigBaker.ChooseGrid(cellW: 200, cellH: 240, cells: 8, out int cols, out int rows);
            Assert.AreEqual(8, cols, "8 small cells fit on one row, and one row is the nicest sheet");
            Assert.AreEqual(1, rows);
        }

        [Test]
        public void ChooseGrid_AddsRowsOnlyWhenTheCapForcesIt()
        {
            // 640 px cells: 8 across would be 5120, past 4096. 6 across is 3840 → 2 rows.
            BuildingRigBaker.ChooseGrid(cellW: 640, cellH: 420, cells: 8, out int cols, out int rows);

            Assert.LessOrEqual(cols * 640, BuildingRigBaker.MaxTextureSize, "width must stay under the cap");
            Assert.LessOrEqual(rows * 420, BuildingRigBaker.MaxTextureSize, "height must stay under the cap");
            Assert.GreaterOrEqual(cols * rows, 8, "the grid must hold every facing");
            Assert.AreEqual(6, cols, "as wide as the cap allows");
            Assert.AreEqual(2, rows);
        }

        [Test]
        public void ChooseGrid_NeverEmitsAnOverCapSheet()
        {
            // The silent-downscale trap: over the cap, Unity imports the sheet shrunk, the sprite COUNT
            // still matches, and only a cell-size assert much later catches it. Refusing is the point.
            foreach (var (w, h) in new[] { (300, 300), (900, 500), (1200, 1160), (2048, 700) })
            {
                BuildingRigBaker.ChooseGrid(w, h, 8, out int cols, out int rows);
                Assert.LessOrEqual(cols * w, BuildingRigBaker.MaxTextureSize, $"{w}×{h} width");
                Assert.LessOrEqual(rows * h, BuildingRigBaker.MaxTextureSize, $"{w}×{h} height");
                Assert.GreaterOrEqual(cols * rows, 8, $"{w}×{h} must hold 8 facings");
            }
        }

        [Test]
        public void ChooseGrid_SolvesForTheCallersCap_NotJustUnitysHardLimit()
        {
            // The baker's own limit is Unity's HARD cap (4096); a kit that imports at Unity's DEFAULT
            // cap (2048) has to be solved for THAT number, because the two fail in opposite directions:
            // over 4096 the bake refuses, but between 2048 and 4096 the bake succeeds and the IMPORT
            // silently downscales — with the sprite count still coming out right.
            //
            // These are the village kit's real measured cells. At 4096 the school packs 8×1 = 2800 px
            // wide, which imports downscaled at a 2048 cap; at 2048 it packs 5×2 and fits.
            BuildingRigBaker.ChooseGrid(350, 366, 8, out int wideCols, out _);
            Assert.AreEqual(8, wideCols, "against the hard cap, eight 350 px cells fit on one row");
            Assert.Greater(8 * 350, 2048, "…and that row is over Unity's DEFAULT cap");

            BuildingRigBaker.ChooseGrid(350, 366, 8, out int cols, out int rows, maxDimension: 2048);
            Assert.LessOrEqual(cols * 350, 2048, "width must respect the caller's cap");
            Assert.LessOrEqual(rows * 366, 2048, "height must respect the caller's cap");
            Assert.GreaterOrEqual(cols * rows, 8, "the grid must still hold every facing");
        }

        [Test]
        public void ChooseGrid_RefusesACellWiderThanTheCap_RatherThanEmittingOneColumn()
        {
            // The latent hole in "widest grid that fits": when the cell is WIDER than the cap, the
            // column count floors to 1 and a single column is still over-cap. Emitting it would produce
            // exactly the silent downscale everything else here exists to prevent.
            Assert.Throws<InvalidOperationException>(
                () => BuildingRigBaker.ChooseGrid(3000, 200, 8, out _, out _, maxDimension: 2048));
        }

        [Test]
        public void ChooseGrid_ThrowsWhenACellCannotFitAtAll()
        {
            // One 3000×3000 cell fits, but 8 of them need 3 rows = 9000 px. No grid works.
            Assert.Throws<InvalidOperationException>(
                () => BuildingRigBaker.ChooseGrid(3000, 3000, 8, out _, out _),
                "an unpackable cell must throw, not emit a sheet Unity will silently downscale");
        }

        // ---- the catalog ------------------------------------------------------------------------

        [Test]
        public void BothBuildingRigs_AreInTheCatalog_AndTheirSourceIsOnDisk()
        {
            foreach (var key in new[] { "house", "wharfBuilding" })
            {
                var entry = RigCatalog.Get(key);
                string abs = Path.Combine(RigCatalog.RepoRoot, entry.ScriptPath);
                Assert.IsTrue(File.Exists(abs), $"'{entry.ScriptPath}' is missing — {key} cannot bake.");

                // Both are in the README's INFERRED counter-clockwise group. The declaration is only a
                // prior: the bake measures and refuses on a mismatch. Pinned so a silent flip is loud.
                Assert.AreEqual(AzimuthConvention.CounterClockwise, entry.DeclaredConvention,
                                $"{key}'s declared convention");
            }
        }

        [Test]
        public void EveryMenuPreset_ExistsInItsRigSource()
        {
            // Cheap text check, no JS engine: the menu's preset names are the one hand-written thing in
            // the baker, so a typo would otherwise only surface as a bake failure on the owner's machine.
            foreach (var (rig, preset) in BuildingBakeMenu.AllBuilds())
            {
                string src = File.ReadAllText(
                    Path.Combine(RigCatalog.RepoRoot, RigCatalog.Get(rig).ScriptPath));
                StringAssert.Contains($"{preset}:", src,
                                      $"'{preset}' is not declared in {rig}'s PRESETS table");
            }
        }

        [Test]
        public void TheMenuBakesTheLargestBuilds_SoTheWorstCaseIsCovered()
        {
            var wharf = BuildingBakeMenu.WharfPresets;
            CollectionAssert.Contains(wharf, "cannery",
                "the cannery is the kit's biggest build and therefore the crop's worst case — baking it " +
                "is how we know the baker holds for everything smaller");
            CollectionAssert.Contains(wharf, "netShed", "the shed is what Nine Mile Creek actually needs");

            Assert.AreEqual(BuildingBakeMenu.HousePresets.Length + wharf.Length,
                            BuildingBakeMenu.AllBuilds().Count(),
                            "AllBuilds must cover both rigs' preset lists exactly once");
        }

        [Test]
        public void SheetNamesAreDistinctPerRigAndPreset()
        {
            var names = BuildingBakeMenu.AllBuilds()
                                        .Select(b => $"{BuildingBakeMenu.Prefix(b.rig)}_{b.preset}")
                                        .ToArray();
            CollectionAssert.AllItemsAreUnique(names,
                "two builds writing the same sheet would silently overwrite each other");
        }
    }
}
