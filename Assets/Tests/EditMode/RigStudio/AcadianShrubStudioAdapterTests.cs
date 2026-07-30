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
    /// Acceptance for the shrub adapter — the abstraction's stress test, so this file pins the three
    /// things the richer axis space could have bent:
    ///
    /// <list type="bullet">
    ///   <item><b>Preview == bake, bit for bit</b>, across the phase × sway grid — same pin as the
    ///   buildings, through the PNG on disk and back.</item>
    ///   <item><b>Snow is annotation, never pixels.</b> The same point at two snow steps must render
    ///   IDENTICAL bytes (the guide line is the only difference) — because sheets bake snow-free and
    ///   the scene clips at placement, a snow-tinted preview would be approving art that never
    ///   ships.</item>
    ///   <item><b>The kit's own laws refuse through the adapter</b>: a buried species refuses with the
    ///   contract's drift ruling; a stage the committed contract was not exported at refuses with the
    ///   baker's own re-export message.</item>
    /// </list>
    ///
    /// <para>CPU-only (V8 host, <c>Texture2D.LoadImage</c>), output to gitignored <c>artifacts/</c>,
    /// deleted after — the shrub cells are 46×39 px, so the full-grid pin is cheap.</para>
    /// </summary>
    public class AcadianShrubStudioAdapterTests
    {
        /// <summary>The smallest species in the kit (0.38 m lowbush, 46×39 cell) — the cheapest
        /// honest pin, and conveniently also one the snow law buries (deep/drift on the barren).</summary>
        const string PinSpecies = "LowbushBlueberry";

        const string OutFolder = "artifacts/rig-studio-test";

        static string RepoRoot => Directory.GetParent(Application.dataPath)!.FullName;

        static Dictionary<string, object> Point(string species = PinSpecies,
                                                string stage = "full", string phase = "leaf",
                                                int variant = 0, int frame = 0,
                                                string snow = "none") =>
            new Dictionary<string, object>
            {
                [AcadianShrubStudioAdapter.SpeciesAxis] = species,
                [AcadianShrubStudioAdapter.StageAxis] = stage,
                [AcadianShrubStudioAdapter.PhaseAxis] = phase,
                [AcadianShrubStudioAdapter.VariantAxis] = variant,
                [AcadianShrubStudioAdapter.FrameAxis] = frame,
                [AcadianShrubStudioAdapter.SnowAxis] = snow,
            };

        // ---- axis enumeration: the contract, never a restatement --------------------------------

        [Test]
        public void EveryAxis_IsEnumeratedFromTheCommittedContract()
        {
            ShrubCatalog.Contract contract = ShrubCatalog.Load();
            Assert.IsNotNull(contract, "the committed shrub contract must load");

            using var adapter = new AcadianShrubStudioAdapter();
            RigStudioSchema schema = adapter.Describe();
            RigStudioAxis Axis(string key) => schema.Axes.Single(a => a.Key == key);

            CollectionAssert.AreEqual(contract.Species.Select(s => s.Key).ToArray(),
                Axis(AcadianShrubStudioAdapter.SpeciesAxis).Keys,
                "species must be the contract's own twenty, in the contract's own order");

            CollectionAssert.AreEqual(contract.Calendar.Phases.Select(p => p.Key).ToArray(),
                Axis(AcadianShrubStudioAdapter.PhaseAxis).Keys,
                "phases must be the contract's calendar, in year order");

            CollectionAssert.AreEqual(contract.Snow.Steps.Select(s => s.Key).ToArray(),
                Axis(AcadianShrubStudioAdapter.SnowAxis).Keys,
                "snow steps must be the contract's snow law");

            Assert.AreEqual(contract.Sway.Frames - 1,
                Axis(AcadianShrubStudioAdapter.FrameAxis).Max,
                "the sway-frame bound is the contract's own frame count");

            var first = contract.Species[0];
            Assert.AreEqual(first.VariantSheetW / first.CellW - 1,
                Axis(AcadianShrubStudioAdapter.VariantAxis).Max,
                "the variant bound is contract geometry — variant-axis sheet width over cell width — " +
                "not a literal 3");
        }

        // ---- THE PIN: preview == bake across the phase × sway grid ------------------------------

        [Test]
        public void Preview_IsBitIdentical_ToThePhaseAxisBake_AcrossPhasesAndSwayFrames()
        {
            ShrubCatalog.Contract contract = ShrubCatalog.Load();
            ShrubCatalog.SpeciesEntry entry = contract.Find(PinSpecies);
            string[] phases = contract.Calendar.Phases.Select(p => p.Key).ToArray();

            Directory.CreateDirectory(Path.Combine(RepoRoot, OutFolder));

            // The real bake, through the kit's own public entry — the same call the menu makes,
            // scoped to the pin species, into the scratch folder.
            ShrubBakeResult baked = ShrubBaker.BakePhaseAxis(
                new[] { PinSpecies }, outputFolder: OutFolder);

            Assert.AreEqual(1, baked.Sheets.Count);
            ShrubSheetBake sheet = baked.Sheets[0];

            // AssetPaths follows ShrubCatalog.Channels order — the albedo is first (empty suffix).
            string albedoPath = sheet.AssetPaths[0];
            StringAssert.StartsWith(OutFolder, albedoPath,
                "the pin bake must land in the scratch folder it asked for");

            var tex = new Texture2D(2, 2);
            try
            {
                Assert.IsTrue(tex.LoadImage(File.ReadAllBytes(Path.Combine(RepoRoot, albedoPath))),
                              "the baked albedo PNG failed to decode");
                Assert.AreEqual(sheet.Width, tex.width);
                Assert.AreEqual(sheet.Height, tex.height);
                Color32[] px = tex.GetPixels32();

                using var adapter = new AcadianShrubStudioAdapter();
                foreach (int frame in new[] { 0, contract.Sway.Frames - 1 })
                    for (int col = 0; col < phases.Length; col++)
                    {
                        RigStudioPreview preview =
                            adapter.Preview(Point(phase: phases[col], frame: frame));

                        Assert.AreEqual(entry.CellW, preview.Width);
                        Assert.AreEqual(entry.CellH, preview.Height);
                        Assert.AreEqual(entry.PivotX, preview.PivotX, 1e-6,
                            "the pivot the owner sees is the contract's root crown");
                        Assert.AreEqual(entry.PivotY, preview.PivotY, 1e-6);

                        AssertCellMatches(px, sheet, entry, preview,
                                          col: col, rowFromTop: frame,
                                          what: $"phase '{phases[col]}' frame {frame}");
                    }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
                foreach (string path in baked.Sheets.SelectMany(s => s.AssetPaths))
                {
                    string abs = Path.Combine(RepoRoot, path);
                    if (File.Exists(abs)) File.Delete(abs);
                }
            }
        }

        static void AssertCellMatches(Color32[] sheetPx, ShrubSheetBake sheet,
                                      ShrubCatalog.SpeciesEntry entry, RigStudioPreview preview,
                                      int col, int rowFromTop, string what)
        {
            int mismatches = 0;
            for (int y = 0; y < entry.CellH; y++)
            {
                int sheetY = sheet.Height - 1 - (rowFromTop * entry.CellH + y);
                int sheetRow = sheetY * sheet.Width + col * entry.CellW;
                int cellRow = y * entry.CellW * 4;

                for (int x = 0; x < entry.CellW; x++)
                {
                    Color32 b = sheetPx[sheetRow + x];
                    int s = cellRow + x * 4;
                    if (b.r != preview.Rgba[s] || b.g != preview.Rgba[s + 1] ||
                        b.b != preview.Rgba[s + 2] || b.a != preview.Rgba[s + 3])
                        mismatches++;
                }
            }

            Assert.AreEqual(0, mismatches,
                $"{what}: {mismatches} pixel(s) differ between the studio preview and the baked " +
                "sheet. Preview and bake have split into two render paths — both must run the kit's " +
                "own ShrubBaker.ResultExpr.");
        }

        // ---- snow: annotation, legality, and never pixels ---------------------------------------

        [Test]
        public void Snow_NeverChangesThePixels_OnlyTheGuideAndTheCaption()
        {
            using var adapter = new AcadianShrubStudioAdapter();

            RigStudioPreview bare = adapter.Preview(Point(snow: "none"));
            RigStudioPreview dusted = adapter.Preview(Point(snow: "dust"));

            CollectionAssert.AreEqual(bare.Rgba, dusted.Rgba,
                "the pixels at 'dust' must BE the pixels at 'none' — sheets bake snow-free and the " +
                "scene clips at placement, so a snow-tinted preview would be approving art that " +
                "never ships");

            Assert.IsNull(bare.Guides, "no snow, no guide");
            Assert.IsNotNull(dusted.Guides, "a snow step draws the kit's surface line");
            Assert.AreEqual(1, dusted.Guides.Length);

            // The line sits depth × PPU above the root crown — the contract's own arithmetic.
            ShrubCatalog.Contract contract = ShrubCatalog.Load();
            float depth = ShrubCatalog.SnowDepthMetres(contract, PinSpecies, "dust");
            Assert.Greater(depth, 0f);
            Assert.AreEqual(contract.Find(PinSpecies).PivotY - depth * ShrubCatalog.Ppu,
                            dusted.Guides[0].Y, 1e-3);
        }

        [Test]
        public void ABuriedSpecies_RefusesWithTheKitsDriftRuling_NotAPreviewOfNothing()
        {
            // Find a combination the contract itself declares illegal rather than hard-coding one —
            // the snow law's numbers are the contract's to change.
            ShrubCatalog.Contract contract = ShrubCatalog.Load();
            var illegal =
                (from sp in contract.Species
                 from step in contract.Snow.Steps
                 where !ShrubCatalog.IsLegalAtSnow(contract, sp.Key, step.Key)
                 select (sp.Key, step.Key)).FirstOrDefault();

            Assert.IsFalse(illegal == default,
                "the committed snow law buries at least one species (LowbushBlueberry at deep was " +
                "1.03 buried when this was written) — if nothing is illegal any more, the law changed " +
                "and this test should be revisited");

            using var adapter = new AcadianShrubStudioAdapter();
            var ex = Assert.Throws<ArgumentException>(
                () => adapter.Preview(Point(species: illegal.Item1, snow: illegal.Item2)),
                $"'{illegal.Item1}' at '{illegal.Item2}' is past the buried cutoff — the kit's " +
                "ruling is a drift, not a shrub, so there is nothing honest to preview");

            StringAssert.Contains("drift", ex.Message,
                "the refusal must carry the kit's own ruling");
        }

        // ---- the kit's guards refuse through the adapter ----------------------------------------

        [Test]
        public void AStageTheContractWasNotExportedAt_RefusesWithTheBakersOwnMessage()
        {
            // Precondition, read from the contract's own provenance line rather than assumed: the
            // committed contract is exported at 'full'. If that ever changes, this test's expectation
            // flips with it — deliberately.
            ShrubCatalog.Contract contract = ShrubCatalog.Load();
            StringAssert.Contains("at full growth stage", contract.Generated,
                "this test assumes the committed contract is the full-stage export; it is not — " +
                "update the stage below to match the new export");

            using var adapter = new AcadianShrubStudioAdapter();
            var ex = Assert.Throws<InvalidOperationException>(
                () => adapter.Preview(Point(stage: "young")),
                "a young-stage cell is one no bake can ship against the committed full-stage " +
                "contract — the preview must refuse exactly where the bake refuses");

            StringAssert.Contains("re-export the contract", ex.Message,
                "the refusal must be the baker's own message, verbatim — the studio adds nothing " +
                "and swallows nothing");
        }
    }
}
