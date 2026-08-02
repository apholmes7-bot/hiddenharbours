using System;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Owner-facing entry points for the Shoreline Plant kit. One click bakes the sheets,
    /// force-reimports and slices them with their ground-contact pivots; nothing further to run.
    ///
    /// <para><b>Two menu items, because the atlas budget is a real decision.</b> The default bakes
    /// the TIDE axis — five tide states × four sway frames, summer, full growth, variant 0 — for all
    /// sixteen species: 16 sheets × 3 channels = 48 PNGs. That is the gameplay-minimum set, because
    /// the tide moves continuously and every plant follows it, so the tide axis is what a placed
    /// plant actually samples. The second item adds the VARIANT axis at one tide state, which buys
    /// bed-to-bed variety and costs another 48 PNGs. Seasons and growth stages multiply from there
    /// and are deliberately not offered as a default: 16 × 2 axes × 3 seasons × 3 stages is a matrix
    /// the owner picks from, not a loop bound.</para>
    ///
    /// <para>The contract's cell and pivot are tide-, season- and variant-independent, so the engine
    /// wiring does not change with the answer — only texture memory does. Both sheet axes at full
    /// growth are 11.42 MB RGBA uncompressed across the sixteen species.</para>
    /// </summary>
    public static class ShorePlantBakeMenu
    {
        public static string OutputFolder => ShorePlantBaker.DefaultOutputFolder;

        [MenuItem("Hidden Harbours/Dev/Bake Shore Plant Sheets (tide axis, summer, full = 16)",
                  priority = 143)]
        public static void BakeTideAxis() =>
            Run(() => ShorePlantBaker.BakeTideAxis(progress: Progress("tide axis")),
                "tide axis · summer · full growth · variant 0");

        [MenuItem("Hidden Harbours/Dev/Bake Shore Plant Sheets — tide AND variant axes (32)",
                  priority = 144)]
        public static void BakeBothAxes()
        {
            if (!EditorUtility.DisplayDialog(
                    "Bake both sheet axes?",
                    "This writes 32 sheets (96 PNGs) — all sixteen species on the tide axis " +
                    "(5 tide states × 4 sway frames) AND the variant axis (4 variants × 4 sway " +
                    "frames) at half tide, summer, full growth.\n\nThe tide axis alone is enough to " +
                    "place a plant that follows the water. The variant axis buys bed-to-bed " +
                    "variety.\n\nCell and pivot are identical either way, so nothing downstream " +
                    "changes — only texture memory.",
                    "Bake both (96 PNGs)", "Cancel"))
                return;

            Run(() =>
            {
                var tide = ShorePlantBaker.BakeTideAxis(progress: Progress("tide axis"));
                var variant = ShorePlantBaker.BakeVariantAxis(progress: Progress("variant axis"));
                tide.Sheets.AddRange(variant.Sheets);
                tide.CellsRendered += variant.CellsRendered;
                tide.RenderMilliseconds += variant.RenderMilliseconds;
                tide.TotalMilliseconds += variant.TotalMilliseconds;
                tide.TotalPngBytes += variant.TotalPngBytes;
                return tide;
            }, "tide + variant axes · summer · full growth");
        }

        static Action<string, float> Progress(string what) =>
            (label, t) => EditorUtility.DisplayProgressBar($"Baking shore plants ({what})", label, t);

        static void Run(Func<PlantBakeResult> bake, string what)
        {
            PlantBakeResult result;
            try
            {
                result = bake();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[rig-baker] shore plant bake FAILED: {ex.Message}\n{ex}");
                throw;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log(Summarise(result, what));

            AssetDatabase.Refresh();

            // ⚠️ Reload every written file from disk BEFORE anything reads a texture: the sheets were
            // written with File.WriteAllBytes behind the AssetDatabase's back, and a mid-build import
            // invalidates any in-memory reference taken before it.
            foreach (string path in result.Sheets.SelectMany(s => s.AssetPaths))
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            ShorePlantSheetSlicer.SliceAllMenu();

            bool verified = ShorePlantSheetSlicer.VerifyAll(logEachPass: false, out int checkedCount);
            Debug.Log(verified
                ? $"[rig-baker] Shore plant sheets verified: {checkedCount} sheet(s) — pivots, " +
                  "slice counts and import settings all agree with the contract."
                : "[rig-baker] ⚠️ Shore plant sheet verification FAILED — see the errors above.");
        }

        static string Summarise(PlantBakeResult result, string what)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[rig-baker] Shore plant bake ({what}) on {result.EngineName}:");
            foreach (var s in result.Sheets) sb.AppendLine("  " + s);
            sb.AppendLine(
                $"  {result.Sheets.Count} sheet(s), {result.CellsRendered} cell(s) rendered in " +
                $"{result.RenderMilliseconds:F0} ms (total {result.TotalMilliseconds:F0} ms), " +
                $"{result.TotalPngBytes / 1024.0:F0} KiB of PNG across " +
                $"{result.Sheets.Sum(s => s.AssetPaths.Count)} file(s).");
            sb.Append(
                $"  {ShorePlantCatalog.ContractFileName} is NOT rewritten — the committed contract " +
                "is the oracle this bake was checked against (see " +
                "ShorePlantBaker.AssertMatchesContract).");
            return sb.ToString();
        }

        /// <summary>Batch entry point for <c>-executeMethod</c>: bake the tide axis, then slice.</summary>
        public static void BakeTideAxisFromCommandLine()
        {
            try
            {
                var result = ShorePlantBaker.BakeTideAxis();
                Debug.Log(Summarise(result, "tide axis · summer · full growth · variant 0"));
                AssetDatabase.Refresh();
                foreach (string path in result.Sheets.SelectMany(s => s.AssetPaths))
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                ShorePlantSheetSlicer.SliceAllFromCommandLine();
            }
            catch (Exception e)
            {
                Debug.LogError($"[rig-baker] batch shore plant bake threw: {e}");
                EditorApplication.Exit(1);
            }
        }
    }
}
