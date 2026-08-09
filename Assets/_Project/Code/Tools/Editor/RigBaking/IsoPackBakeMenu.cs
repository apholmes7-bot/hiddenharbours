using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Owner-facing entry points for the ISO rig pack — the wharf structure kit, the deck gear, the
    /// village services and the shoreline finds. One click bakes, force-reimports and slices; nothing
    /// further to run. Same philosophy as <see cref="CatchStorageBakeMenu"/>: the RECIPE is a fixed
    /// decision (which family, every key it declares), and every number — cell, pivot, grid — comes from
    /// the committed contract at bake time.
    ///
    /// <para><b>The inventory these four items produce</b> (what a pixel audit counts):</para>
    /// <list type="bullet">
    ///   <item><b>wharfIso</b> — 17 presets → 17 sheets, 8 facings each. Cap 4096.</item>
    ///   <item><b>wharfDecor</b> — 61 pieces → 61 sheets, 8 facings each. Cap 2048.</item>
    ///   <item><b>utilityIso</b> — 42 services → 42 sheets, 8 facings each. Cap 2048.</item>
    ///   <item><b>shoreFinds</b> — 36 finds × 3 states → <b>108 sheets</b>, 24 cells each
    ///         (8 lie angles × 3 variants). Cap 2048.</item>
    /// </list>
    /// <para><b>228 sheets in total.</b> The finds are two thirds of that and by far the longest bake.</para>
    ///
    /// <para><b>Nothing here is silent.</b> A key the rig does not know, a cell that disagrees with the
    /// contract, a sheet that would import over its family's cap — each throws and takes the whole bake
    /// down. A bake that quietly drops sheets still reports success, and the COUNT is the thing the
    /// audit checks.</para>
    /// </summary>
    public static class IsoPackBakeMenu
    {
        public const string WharfIsoFolder = "Assets/_Project/Art/Sprites/Wharf/Iso";
        public const string WharfDecorFolder = "Assets/_Project/Art/Sprites/Wharf/Decor";
        public const string UtilityIsoFolder = "Assets/_Project/Art/Sprites/Utility";
        public const string ShoreFindsFolder = ShoreFindsSheetBaker.DefaultOutputFolder;

        // ---- menu ------------------------------------------------------------------------------------

        [MenuItem("Hidden Harbours/Art/Bake Iso Rig Pack (wharf + decor + utility + finds = 228 sheets)",
                  priority = 61)]
        public static void BakeIsoRigPack() =>
            RunBakes(("wharfIso", BakeWharfIsoInternal),
                     ("wharfDecor", BakeWharfDecorInternal),
                     ("utilityIso", BakeUtilityIsoInternal),
                     ("shoreFinds", BakeShoreFindsInternal));

        [MenuItem("Hidden Harbours/Art/Bake Wharf Iso Kit (17 presets × 8 dir)", priority = 62)]
        public static void BakeWharfIsoKit() => RunBakes(("wharfIso", BakeWharfIsoInternal));

        [MenuItem("Hidden Harbours/Art/Bake Wharf Decor Sheets (61 pieces × 8 dir)", priority = 63)]
        public static void BakeWharfDecorSheets() => RunBakes(("wharfDecor", BakeWharfDecorInternal));

        [MenuItem("Hidden Harbours/Art/Bake Utility Iso Sheets (42 services × 8 dir)", priority = 64)]
        public static void BakeUtilityIsoSheets() => RunBakes(("utilityIso", BakeUtilityIsoInternal));

        [MenuItem("Hidden Harbours/Art/Bake Shore Finds Sheets (36 finds × 3 states × 8 lie × 3 variants)",
                  priority = 65)]
        public static void BakeShoreFindsSheets() => RunBakes(("shoreFinds", BakeShoreFindsInternal));

        // ---- the bakes -------------------------------------------------------------------------------

        /// <summary>What one family's bake produced, for the log line and the reimport pass.</summary>
        public sealed class FamilyBakeResult
        {
            public string Family;
            public readonly List<string> AssetPaths = new List<string>();
            public string Widest = "";
            public int WidestMaxDim;

            public void Record(string assetPath, int sheetW, int sheetH, string key)
            {
                AssetPaths.Add(assetPath);
                int m = Mathf.Max(sheetW, sheetH);
                if (m <= WidestMaxDim) return;
                WidestMaxDim = m;
                Widest = $"{key} {sheetW}×{sheetH}";
            }
        }

        static FamilyBakeResult BakeWharfIsoInternal() => BakeDirectional("wharfIso", WharfIsoFolder);
        static FamilyBakeResult BakeWharfDecorInternal() => BakeDirectional("wharfDecor", WharfDecorFolder);
        static FamilyBakeResult BakeUtilityIsoInternal() => BakeDirectional("utilityIso", UtilityIsoFolder);

        /// <summary>
        /// Bake one of the three directional families. <c>wharfIso</c> goes through
        /// <see cref="WharfIsoSheetBaker"/> and the two fixed-sheet families through
        /// <see cref="IsoPropSheetBaker"/> — they are separate bakers because the wharf kit sizes its own
        /// buffer per bake and reports a FRACTIONAL pivot with it, which the fixed-sheet crop cannot
        /// express.
        /// </summary>
        static FamilyBakeResult BakeDirectional(string family, string folder)
        {
            var contract = IsoPackContract.Load(family);
            var result = new FamilyBakeResult { Family = family };

            using IRigScriptHost host = RigScriptHostFactory.Create();

            // Bake EVERY key the contract measures, in its own order — the recipe is "all of them", so
            // there is no list here to drift out of step with the rig.
            var keys = contract.Cells.Select(c => c.key).ToList();
            for (int i = 0; i < keys.Count; i++)
            {
                string key = keys[i];
                EditorUtility.DisplayProgressBar($"Baking {family}", $"{key} ({i + 1}/{keys.Count})",
                                                 (i + 1) / (float)keys.Count);

                if (family == WharfIsoSheetBaker.RigKey)
                {
                    var r = WharfIsoSheetBaker.Bake(new WharfIsoBakeRequest(key, folder), host);
                    result.Record(r.AssetPath, r.SheetWidth, r.SheetHeight, key);
                }
                else
                {
                    var r = IsoPropSheetBaker.Bake(new IsoPropBakeRequest(family, key, folder), host);
                    result.Record(r.AssetPath, r.SheetWidth, r.SheetHeight, key);
                }
            }

            AssertCountMatches(result, contract.Count, family);
            return result;
        }

        /// <summary>
        /// Bake the finds: one sheet per find per weathering state. The rig is installed ONCE and every
        /// sheet rides that host — 108 sheets × 24 cells is 2,592 renders, and paying the install per
        /// sheet would dominate the bake.
        /// </summary>
        static FamilyBakeResult BakeShoreFindsInternal()
        {
            var contract = IsoPackContract.Load(ShoreFindsSheetBaker.RigKey);
            var result = new FamilyBakeResult { Family = ShoreFindsSheetBaker.RigKey };

            using IRigScriptHost host = RigScriptHostFactory.Create();
            ShoreFindsSheetBaker.Install(host, contract);

            var finds = contract.Cells.Select(c => c.key).ToList();
            var states = contract.StateNames;
            int total = finds.Count * states.Count, done = 0;

            foreach (string find in finds)
            {
                foreach (string state in states)
                {
                    done++;
                    EditorUtility.DisplayProgressBar("Baking shoreFinds",
                                                     $"{find} · {state} ({done}/{total})",
                                                     done / (float)total);

                    var r = ShoreFindsSheetBaker.Bake(
                        new ShoreFindsBakeRequest(find, state, ShoreFindsFolder), host, contract);
                    result.Record(r.AssetPath, r.SheetWidth, r.SheetHeight, $"{find}_{state}");
                }
            }

            AssertCountMatches(result, total, ShoreFindsSheetBaker.RigKey);
            return result;
        }

        /// <summary>
        /// A bake that produced fewer sheets than it promised has dropped some, and the drop is what the
        /// pixel audit would otherwise have to catch by eye.
        /// </summary>
        static void AssertCountMatches(FamilyBakeResult result, int expected, string family)
        {
            if (result.AssetPaths.Count == expected) return;
            throw new InvalidOperationException(
                $"{family} baked {result.AssetPaths.Count} sheets but its contract calls for {expected}. " +
                "A short bake still reports success — refusing instead.");
        }

        // ---- run + slice -------------------------------------------------------------------------------

        static void RunBakes(params (string Family, Func<FamilyBakeResult> Bake)[] bakes)
        {
            var results = new List<FamilyBakeResult>();
            try
            {
                foreach (var (family, bake) in bakes)
                {
                    var r = bake();
                    results.Add(r);
                    Debug.Log($"[rig-baker] {family}: {r.AssetPaths.Count} sheet(s), widest {r.Widest} " +
                              $"({r.WidestMaxDim} px on the longest side).");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[rig-baker] iso-pack bake FAILED: {ex.Message}\n{ex}");
                throw;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();

            // Reload from disk before anything reads these as sprites — the sheets were written with
            // File.WriteAllBytes behind the AssetDatabase's back.
            foreach (var r in results)
                foreach (string path in r.AssetPaths)
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            // Slice in the same operation. ArtImportPipeline stamps the pixel-art import lock on first
            // import; IsoPackSheetSlicer adds the Multiple-mode grid and each piece's own pivot, reading
            // both from the same contracts these bakes just asserted against.
            //
            // ⚠️ This is not optional politeness. A freshly imported sheet is spriteMode Multiple with
            // EMPTY rects, and a consumer then loads NOTHING from it — the sheet looks fine on disk and
            // in the inspector. Bake and slice ship together or the bake is not usable.
            foreach (var r in results)
            {
                int n = Art.Editor.IsoPackSheetSlicer.SliceFamily(r.Family, out int skipped, out int failed);
                Debug.Log($"[rig-baker] {r.Family}: sliced {n} sheet(s) ({skipped} skipped, {failed} failed).");
                if (failed > 0)
                    Debug.LogError($"[rig-baker] {r.Family}: {failed} sheet(s) FAILED to slice — the PNGs " +
                                   "are on disk but carry no usable sprite rects. Do not commit them yet.");
            }

            Debug.Log("[rig-baker] Baked and sliced. Nothing further to run — but the results must be " +
                      "COMMITTED: every *.png + its .meta (LFS covers *.png) under " +
                      $"{WharfIsoFolder}/, {WharfDecorFolder}/, {UtilityIsoFolder}/ and {ShoreFindsFolder}/.");
        }

        /// <summary>
        /// Headless entry point for CI / <c>-executeMethod</c>.
        ///
        /// <para>⚠️ Never invoke this with <c>-quit</c> alongside <c>-runTests</c> — the two race and exit
        /// 0 with total=0, which reads as a pass (ADR 0021).</para>
        /// </summary>
        public static void BakeIsoRigPackFromCommandLine()
        {
            try
            {
                BakeIsoRigPack();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[rig-baker] headless iso-pack bake failed: {ex}");
                EditorApplication.Exit(1);
            }
        }
    }
}
