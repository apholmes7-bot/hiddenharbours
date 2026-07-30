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
    /// Acceptance for the tree adapter. Two things are this file's own; everything generic (schema,
    /// sabotage, the excluded-Tamarack refusal with the kit's reason verbatim) already runs via the
    /// registry-driven contract tests:
    ///
    /// <list type="bullet">
    ///   <item><b>The hold-back is visible, reasoned and unbakeable</b> — Tamarack appears as an
    ///   EXCLUDED value carrying the coordinator ruling, and is never in the selectable set.</item>
    ///   <item><b>Preview == bake bit-identity</b> across the variant columns, against a real bake of
    ///   one species into gitignored <c>artifacts/</c> (this kit's bake also writes its contract
    ///   there, so the committed <c>Trees.json</c> is untouched).</item>
    /// </list>
    /// </summary>
    public class AcadianTreeStudioAdapterTests
    {
        /// <summary>The smallest kept species (79×141 cell) — the cheapest honest pin.</summary>
        const string PinSpecies = "WhiteCedar";

        const string OutFolder = "artifacts/rig-studio-test";

        static string RepoRoot => Directory.GetParent(Application.dataPath)!.FullName;

        static Dictionary<string, object> Point(string species = PinSpecies, int variant = 0) =>
            new Dictionary<string, object>
            {
                [AcadianTreeStudioAdapter.SpeciesAxis] = species,
                [AcadianTreeStudioAdapter.StageAxis] = TreeRigBaker.DefaultStage,
                [AcadianTreeStudioAdapter.SeasonAxis] = TreeRigBaker.DefaultSeason,
                [AcadianTreeStudioAdapter.VariantAxis] = variant,
            };

        [Test]
        public void TheSpeciesAxis_IsTheCommittedContract_AndTheHoldBack_IsExcludedWithTheRuling()
        {
            TreeKitCatalog.Contract contract = TreeKitCatalog.Load();
            Assert.IsNotNull(contract);

            using var adapter = new AcadianTreeStudioAdapter();
            RigStudioAxis axis = adapter.Describe().Axes
                .Single(a => a.Key == AcadianTreeStudioAdapter.SpeciesAxis);

            CollectionAssert.AreEqual(
                contract.trees.Select(e => e.species).Distinct().ToArray(), axis.Keys,
                "the selectable species are exactly the contract's — the held-back one is absent " +
                "from Trees.json by design, so it cannot be in this list");

            foreach (string held in TreeKitCatalog.HeldBackSpecies)
            {
                Assert.IsNotNull(axis.Excluded, "the hold-back must be SHOWN, not hidden");
                Assert.IsTrue(axis.Excluded.ContainsKey(held),
                    $"'{held}' must appear as excluded — hidden, the hold-back reads as missing art");
                StringAssert.Contains("held back", axis.Excluded[held]);
                CollectionAssert.DoesNotContain(axis.Keys, held);
            }
        }

        [Test]
        public void TheVariantBound_IsTheContractsOwnColumnCount()
        {
            TreeKitCatalog.Contract contract = TreeKitCatalog.Load();
            using var adapter = new AcadianTreeStudioAdapter();

            Assert.AreEqual(contract.sheet.cols - 1,
                adapter.Describe().Axes
                    .Single(a => a.Key == AcadianTreeStudioAdapter.VariantAxis).Max);
        }

        [Test]
        public void Preview_IsBitIdentical_ToTheBakeAtTheSamePoint_AcrossVariants()
        {
            Directory.CreateDirectory(Path.Combine(RepoRoot, OutFolder));

            // A real bake of the pin species into the scratch folder — sheets AND this bake's own
            // Trees.json land there, leaving the committed kit untouched.
            TreeBakeResult baked = TreeRigBaker.Bake(new[] { PinSpecies }, outputFolder: OutFolder);
            TreeSheetBake albedo = baked.Sheets
                .Single(s => s.Channel == TreeKitCatalog.Channel.Albedo);
            TreeKitCatalog.Entry entry = TreeKitCatalog.Find(baked.Contract, PinSpecies,
                                                            TreeRigBaker.DefaultStage);
            Assert.IsNotNull(entry);

            var tex = new Texture2D(2, 2);
            try
            {
                Assert.IsTrue(tex.LoadImage(
                    File.ReadAllBytes(Path.Combine(RepoRoot, albedo.AssetPath))));
                Assert.AreEqual(albedo.Width, tex.width);
                Assert.AreEqual(albedo.Height, tex.height);
                Color32[] px = tex.GetPixels32();

                using var adapter = new AcadianTreeStudioAdapter();
                for (int variant = 0; variant < albedo.Cols; variant++)
                {
                    RigStudioPreview preview = adapter.Preview(Point(variant: variant));

                    Assert.AreEqual(entry.cellW, preview.Width);
                    Assert.AreEqual(entry.cellH, preview.Height);

                    // The cross the owner sees is the kit's trunk-foot row — pad up from the floor
                    // (ADR 0026: the same fraction is the wind shader's _TrunkAnchor).
                    Assert.AreEqual(entry.cellH - entry.nearFlarePad, preview.PivotY, 1e-6,
                        "the preview pivot must be the tree kit's own pad convention");

                    int mismatches = CountMismatches(px, albedo, entry, preview, col: variant);
                    Assert.AreEqual(0, mismatches,
                        $"variant {variant}: {mismatches} pixel(s) differ between the studio " +
                        "preview and the baked sheet — the two render paths have split; both must " +
                        "run TreeRigBaker.ResultExpr/ChannelExpr.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
                foreach (string path in baked.Sheets.Select(s => s.AssetPath)
                                             .Append(baked.ContractPath))
                {
                    string abs = Path.Combine(RepoRoot, path);
                    if (File.Exists(abs)) File.Delete(abs);
                }
            }
        }

        static int CountMismatches(Color32[] sheetPx, TreeSheetBake sheet,
                                   TreeKitCatalog.Entry entry, RigStudioPreview preview, int col)
        {
            int mismatches = 0;
            for (int y = 0; y < entry.cellH; y++)
            {
                int sheetY = sheet.Height - 1 - y;             // one sway row baked — row 0
                int sheetRow = sheetY * sheet.Width + col * entry.cellW;
                int cellRow = y * entry.cellW * 4;

                for (int x = 0; x < entry.cellW; x++)
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
