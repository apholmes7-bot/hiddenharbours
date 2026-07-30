using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Tools.RigBaking;
using HiddenHarbours.Tools.RigStudio;

namespace HiddenHarbours.Tests.RigStudio
{
    /// <summary>
    /// Acceptance for the buildings adapter — and above all the <b>preview == bake bit-identity
    /// pin</b>: the pixels the Rig Studio shows the owner ARE the pixels a bake at that point writes,
    /// byte for byte, through the PNG on disk and back.
    ///
    /// <para>That pin is the flick-cast lesson made structural. A preview renderer that can drift
    /// from the bake means the owner approves art that never ships; the adapter therefore renders
    /// through <see cref="BuildingRigBaker.RenderCells"/> — the bake's own path — and this test
    /// fails any refactor that quietly splits the two.</para>
    ///
    /// <para>CPU-only: the V8 host needs no graphics device, and the PNG round-trip is
    /// <c>Texture2D.LoadImage</c>/<c>GetPixels32</c> — the same arrangement the tree kit's
    /// bit-exactness test already runs on CI. Output goes to the gitignored <c>artifacts/</c>
    /// folder, same as the shrub bake test, and is deleted after.</para>
    /// </summary>
    public class VillageBuildingStudioAdapterTests
    {
        /// <summary>The smallest build in the set (size 0.15) — the cheapest honest pin. The path
        /// under test is build-independent: request → RenderCells → pack, so one build pins it.</summary>
        const string PinBuild = "school";

        const string OutFolder = "artifacts/rig-studio-test";

        static string RepoRoot => Directory.GetParent(Application.dataPath)!.FullName;

        static Dictionary<string, object> Point(string build, int facing) =>
            new Dictionary<string, object>
            {
                [VillageBuildingStudioAdapter.BuildAxis] = build,
                [VillageBuildingStudioAdapter.FacingAxis] = facing,
            };

        // ---- axis enumeration: the kit's own tables, never a restatement -------------------------

        [Test]
        public void TheBuildAxis_IsExactlyTheKitsM1Set_InTheKitsOrder()
        {
            using var adapter = new VillageBuildingStudioAdapter();
            RigStudioAxis axis = adapter.Describe().Axes
                .Single(a => a.Key == VillageBuildingStudioAdapter.BuildAxis);

            CollectionAssert.AreEqual(
                VillageBuildingKit.M1Set.Select(b => b.Key).ToArray(), axis.Keys,
                "the adapter's build axis must BE VillageBuildingKit.M1Set — same keys, same " +
                "order. A restated list is a list that drifts, and a drifted entry either offers " +
                "a build the kit cannot bake or hides one it can.");
        }

        [Test]
        public void TheFacingAxis_BoundsComeFromTheKitsOwnFacingCount()
        {
            using var adapter = new VillageBuildingStudioAdapter();
            RigStudioAxis axis = adapter.Describe().Axes
                .Single(a => a.Key == VillageBuildingStudioAdapter.FacingAxis);

            Assert.AreEqual(0, axis.Min);
            Assert.AreEqual(VillageBuildingKit.Facings - 1, axis.Max,
                "the facing bound is VillageBuildingKit.Facings — the kit's number, not a literal 7");
        }

        // ---- the sharpest form of the lie --------------------------------------------------------

        [Test]
        public void ARealRigPresetThatIsNotInTheKit_IsRefused_NotQuietlyRendered()
        {
            // 'whiteFarmhouse' is a rig preset AND a kit build; 'shingleCottage' is a real preset of
            // the same rig that the kit deliberately left out (not clapboard). The generic sabotage
            // test uses an obvious nonsense value — this is the dangerous cousin: a value that LOOKS
            // right, exists in the rig, and would render beautifully. The kit says no, so the studio
            // must say no.
            using var adapter = new VillageBuildingStudioAdapter();
            Assert.Throws<ArgumentException>(
                () => adapter.Preview(Point("shingleCottage", 0)),
                "'shingleCottage' is a house-rig preset but NOT a VillageBuildingKit build — " +
                "previewing it would show the owner a building the kit will never bake");
        }

        // ---- THE PIN: preview == bake, bit for bit ----------------------------------------------

        [Test]
        public void Preview_IsBitIdentical_ToTheBakeAtTheSamePoint_ForEveryFacing()
        {
            VillageBuildingKit.Build build = VillageBuildingKit.FindBuild(PinBuild).Value;
            Directory.CreateDirectory(Path.Combine(RepoRoot, OutFolder));

            // The real bake, through the kit's own request — sheet PNG + sidecar on disk, exactly
            // what Hidden Harbours ▸ Art ▸ Bake Village Buildings writes for this build.
            BuildingBakeRequest request = VillageBuildingBakeMenu.RequestFor(
                build, OutFolder, "Village_school_studio_pin");
            BuildingBakeResult baked = BuildingRigBaker.Bake(request);

            StringAssert.StartsWith(OutFolder, baked.AssetPath,
                "the pin bake must land in the gitignored scratch folder it asked for, never the " +
                "kit's real one — a request whose outputFolder is silently ignored would pollute " +
                "the committed kit on every test run");

            var sheet = new Texture2D(2, 2);
            try
            {
                byte[] png = File.ReadAllBytes(Path.Combine(RepoRoot, baked.AssetPath));
                Assert.IsTrue(sheet.LoadImage(png), "the baked PNG failed to decode");
                Assert.AreEqual(baked.SheetWidth, sheet.width, "decoded sheet width");
                Assert.AreEqual(baked.SheetHeight, sheet.height, "decoded sheet height");

                Color32[] px = sheet.GetPixels32();

                using var adapter = new VillageBuildingStudioAdapter();
                for (int facing = 0; facing < baked.Facings; facing++)
                {
                    RigStudioPreview preview = adapter.Preview(Point(PinBuild, facing));

                    Assert.AreEqual(baked.CellWidth, preview.Width, $"facing {facing}: cell width");
                    Assert.AreEqual(baked.CellHeight, preview.Height, $"facing {facing}: cell height");

                    // The pivot the owner sees is the pivot the slicer will stamp.
                    Assert.AreEqual(baked.PivotX, preview.PivotX, 1e-6, $"facing {facing}: pivotX");
                    Assert.AreEqual(baked.PivotY, preview.PivotY, 1e-6, $"facing {facing}: pivotY");

                    AssertFacingMatches(baked, px, preview, facing);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sheet);
                TryDelete(baked.AssetPath);
                TryDelete(baked.SidecarPath);
            }
        }

        /// <summary>Compare one preview against the same facing's cell in the baked sheet — every
        /// channel of every pixel. The sheet decodes bottom-origin; the preview (like the rigs) is
        /// top-left-origin rows, so the y flip here mirrors the baker's blit exactly.</summary>
        static void AssertFacingMatches(BuildingBakeResult baked, Color32[] sheetPx,
                                        RigStudioPreview preview, int facing)
        {
            int col = facing % baked.Columns;
            int rowFromTop = facing / baked.Columns;

            int mismatches = 0;
            int firstX = -1, firstY = -1;

            for (int y = 0; y < baked.CellHeight; y++)
            {
                int sheetY = baked.SheetHeight - 1 - (rowFromTop * baked.CellHeight + y);
                int sheetRow = sheetY * baked.SheetWidth + col * baked.CellWidth;
                int cellRow = y * baked.CellWidth * 4;

                for (int x = 0; x < baked.CellWidth; x++)
                {
                    Color32 b = sheetPx[sheetRow + x];
                    int s = cellRow + x * 4;

                    if (b.r != preview.Rgba[s] || b.g != preview.Rgba[s + 1] ||
                        b.b != preview.Rgba[s + 2] || b.a != preview.Rgba[s + 3])
                    {
                        if (mismatches == 0) { firstX = x; firstY = y; }
                        mismatches++;
                    }
                }
            }

            Assert.AreEqual(0, mismatches,
                $"facing {facing}: {mismatches} pixel(s) differ between the studio preview and the " +
                $"baked sheet (first at ({firstX},{firstY}) in the cell). The preview and the bake " +
                "have split into two render paths — the owner is approving art that will not ship. " +
                "Both must go through BuildingRigBaker.RenderCells.");
        }

        static void TryDelete(string projectRelative)
        {
            if (string.IsNullOrEmpty(projectRelative)) return;
            string abs = Path.Combine(RepoRoot, projectRelative);
            if (File.Exists(abs)) File.Delete(abs);
        }
    }
}
