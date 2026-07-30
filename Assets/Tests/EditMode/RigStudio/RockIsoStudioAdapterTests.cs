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
    /// Acceptance for the rock adapter — the thin one, so this file pins only what is its own: axis
    /// enumeration against <c>RockIso.json</c>, the per-species variant refusal (the one cross-axis
    /// wrinkle this kit adds), and the preview==bake bit-identity across a whole variants × dress
    /// grid. Generic schema/sabotage coverage runs via the registry tests.
    /// </summary>
    public class RockIsoStudioAdapterTests
    {
        /// <summary>The smallest form in the kit (52×32 cell, 3 variants) — the cheapest honest pin.</summary>
        const string PinSpecies = "Cobble";

        const string OutFolder = "artifacts/rig-studio-test";

        static string RepoRoot => Directory.GetParent(Application.dataPath)!.FullName;

        static Dictionary<string, object> Point(string species = PinSpecies,
                                                string stone = "sandstone", string tide = "dry",
                                                string dress = "bare", int variant = 0) =>
            new Dictionary<string, object>
            {
                [RockIsoStudioAdapter.SpeciesAxis] = species,
                [RockIsoStudioAdapter.StoneAxis] = stone,
                [RockIsoStudioAdapter.TideAxis] = tide,
                [RockIsoStudioAdapter.DressAxis] = dress,
                [RockIsoStudioAdapter.VariantAxis] = variant,
            };

        [Test]
        public void EveryAxis_IsEnumeratedFromTheCommittedContract()
        {
            RockIsoCatalog.Contract contract = RockIsoCatalog.Load();
            Assert.IsNotNull(contract);

            using var adapter = new RockIsoStudioAdapter();
            RigStudioSchema schema = adapter.Describe();
            RigStudioAxis Axis(string key) => schema.Axes.Single(a => a.Key == key);

            CollectionAssert.AreEqual(contract.Species.Select(s => s.Key).ToArray(),
                                      Axis(RockIsoStudioAdapter.SpeciesAxis).Keys);
            CollectionAssert.AreEqual(contract.Stones, Axis(RockIsoStudioAdapter.StoneAxis).Keys);
            CollectionAssert.AreEqual(contract.Tides, Axis(RockIsoStudioAdapter.TideAxis).Keys);
            CollectionAssert.AreEqual(contract.Dress, Axis(RockIsoStudioAdapter.DressAxis).Keys);

            Assert.AreEqual(contract.Species.Max(s => s.Variants.Count) - 1,
                            Axis(RockIsoStudioAdapter.VariantAxis).Max,
                            "the variant bound is the WIDEST form's contract columns");
        }

        [Test]
        public void AVariantTheViewedFormDoesNotHave_RefusesWithItsRealCount()
        {
            RockIsoCatalog.Contract contract = RockIsoCatalog.Load();
            int widest = contract.Species.Max(s => s.Variants.Count);
            RockIsoCatalog.SpeciesEntry narrower =
                contract.Species.FirstOrDefault(s => s.Variants.Count < widest);
            if (narrower == null)
                Assert.Ignore("every form has the same variant count — nothing to refuse");

            using var adapter = new RockIsoStudioAdapter();
            var ex = Assert.Throws<ArgumentException>(
                () => adapter.Preview(Point(species: narrower.Key,
                                            variant: narrower.Variants.Count)),
                $"'{narrower.Key}' has {narrower.Variants.Count} variant(s) — a higher index must " +
                "refuse, not silently render some other rock");

            StringAssert.Contains(narrower.Variants.Count.ToString(), ex.Message,
                "the refusal must name the form's own count");
        }

        [Test]
        public void Preview_IsBitIdentical_ToTheBakeAtTheSamePoint_AcrossVariantsAndDress()
        {
            RockIsoCatalog.Contract contract = RockIsoCatalog.Load();
            RockIsoCatalog.SpeciesEntry entry = contract.Find(PinSpecies);

            Directory.CreateDirectory(Path.Combine(RepoRoot, OutFolder));

            RockBakeResult baked = RockIsoBaker.Bake(
                stones: new[] { "sandstone" }, tides: new[] { "dry" },
                species: new[] { PinSpecies }, outputFolder: OutFolder);

            Assert.AreEqual(1, baked.Sheets.Count);
            RockSheetBake sheet = baked.Sheets[0];
            StringAssert.StartsWith(OutFolder, sheet.AssetPath,
                "the pin bake must land in the scratch folder it asked for");

            var tex = new Texture2D(2, 2);
            try
            {
                Assert.IsTrue(tex.LoadImage(
                    File.ReadAllBytes(Path.Combine(RepoRoot, sheet.AssetPath))));
                Assert.AreEqual(sheet.Width, tex.width);
                Assert.AreEqual(sheet.Height, tex.height);
                Color32[] px = tex.GetPixels32();

                using var adapter = new RockIsoStudioAdapter();
                for (int d = 0; d < contract.Dress.Length; d++)
                    for (int v = 0; v < entry.Variants.Count; v++)
                    {
                        RigStudioPreview preview = adapter.Preview(
                            Point(dress: contract.Dress[d], variant: v));

                        Assert.AreEqual(entry.CellW, preview.Width);
                        Assert.AreEqual(entry.CellH, preview.Height);
                        Assert.AreEqual(entry.Variants[v].Pivot.x, preview.PivotX, 1e-6,
                            "the pivot the owner sees is this VARIANT's own ground contact");
                        Assert.AreEqual(entry.Variants[v].Pivot.y, preview.PivotY, 1e-6);

                        int mismatches = CountMismatches(px, sheet, entry, preview,
                                                         col: v, rowFromTop: d);
                        Assert.AreEqual(0, mismatches,
                            $"{PinSpecies} v{v} dress '{contract.Dress[d]}': {mismatches} " +
                            "pixel(s) differ between preview and bake — the render paths have " +
                            "split; both must run RockIsoBaker.ResultExpr.");
                    }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
                foreach (RockSheetBake s in baked.Sheets)
                {
                    string abs = Path.Combine(RepoRoot, s.AssetPath);
                    if (File.Exists(abs)) File.Delete(abs);
                }
            }
        }

        static int CountMismatches(Color32[] sheetPx, RockSheetBake sheet,
                                   RockIsoCatalog.SpeciesEntry entry, RigStudioPreview preview,
                                   int col, int rowFromTop)
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
            return mismatches;
        }
    }
}
