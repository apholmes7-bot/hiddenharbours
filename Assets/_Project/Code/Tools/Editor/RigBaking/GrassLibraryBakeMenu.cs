using System;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Owner-facing entry point for the grass library. One click bakes every variant, re-imports
    /// them with the bottom-centre pivot, and rewrites the manifest the paint tool and the St Peters
    /// builder read; nothing further to run.
    ///
    /// <para><b>One menu item, because there is no budget decision here.</b> Unlike the shore plant
    /// kit — 16 species × 2 sheet axes × 3 seasons × 3 growth stages, a matrix the owner picks a
    /// slice out of — the grass library is static sprites with no axes at all. The whole set is 26
    /// small PNGs; there is nothing to choose, so nothing is offered.</para>
    ///
    /// <para><b>⚠ Re-baking does not touch the three shipped tufts.</b> <c>GrassTuft.png</c>,
    /// <c>_Short</c> and <c>_Tall</c> predate every rig here, the owner has painted with them, and
    /// they are what the ramp was read off. They appear in the manifest and are never rewritten.</para>
    /// </summary>
    public static class GrassLibraryBakeMenu
    {
        public static string OutputFolder => GrassLibraryBaker.DefaultOutputFolder;

        [MenuItem("Hidden Harbours/Dev/Bake Grass Library (habitat + species = 26)", priority = 145)]
        public static void Bake() => Run(fromCommandLine: false);

        /// <summary>Batch entry point for <c>-executeMethod</c>.</summary>
        public static void BakeFromCommandLine()
        {
            try { Run(fromCommandLine: true); }
            catch (Exception e)
            {
                Debug.LogError($"[rig-baker] batch grass library bake threw: {e}");
                EditorApplication.Exit(1);
            }
        }

        static void Run(bool fromCommandLine)
        {
            GrassBakeResult result;
            try
            {
                result = GrassLibraryBaker.BakeAll(progress: (label, t) =>
                {
                    if (!fromCommandLine)
                        EditorUtility.DisplayProgressBar("Baking grass library", label, t);
                });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[rig-baker] grass library bake FAILED: {ex.Message}\n{ex}");
                throw;
            }
            finally
            {
                if (!fromCommandLine) EditorUtility.ClearProgressBar();
            }

            Debug.Log(Summarise(result));

            AssetDatabase.Refresh();

            // ⚠️ Reload every written file from disk BEFORE anything reads a texture: the PNGs were
            // written with File.WriteAllBytes behind the AssetDatabase's back, and a mid-build
            // import invalidates any in-memory reference taken before it.
            foreach (string path in result.Variants.Select(v => v.AssetPath))
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            int retouched = result.Variants.Count(v => GrassLibraryBaker.ApplyImportSettings(v.AssetPath));
            AssetDatabase.ImportAsset(result.ManifestPath, ImportAssetOptions.ForceUpdate);

            Debug.Log(Verify(result, retouched));
        }

        /// <summary>
        /// Reads the library back the way its CONSUMERS will and reports what they would see. The
        /// bake asserting its own output proves nothing about the import — a PNG can be on disk,
        /// correct, and still yield no sprite because its importer settings are wrong.
        /// </summary>
        static string Verify(GrassBakeResult result, int retouched)
        {
            var lib = GrassLibraryCatalog.Load();
            if (lib == null)
                return "[rig-baker] ⚠️ the grass manifest did not load back — see the error above.";

            var imported = GrassLibraryCatalog.Imported(lib);
            var missing = lib.Entries.Except(imported).ToList();

            var sb = new StringBuilder();
            sb.AppendLine(
                $"[rig-baker] Grass library verified: {imported.Count}/{lib.Entries.Count} entries " +
                $"resolve to an imported sprite ({retouched} needed import settings applied).");
            foreach (string c in GrassLibraryCatalog.HeightClasses)
                sb.AppendLine($"    {c,-7} {imported.Count(e => e.HeightClass == c),2}  " +
                              string.Join(", ", imported.Where(e => e.HeightClass == c)
                                                        .Select(e => $"{e.Name}({e.Climb})")));
            foreach (var h in lib.Habitats.Keys.OrderBy(k => k))
                sb.AppendLine($"    #{h,-10} {imported.Count(e => e.HasHabitat(h)),2}");

            if (missing.Count > 0)
                sb.AppendLine(
                    $"  ⚠️ {missing.Count} entry/entries in the manifest have no imported sprite: " +
                    string.Join(", ", missing.Select(e => e.Name)) +
                    ". The three shipped tufts are LFS-backed — `git lfs pull` if they are pointers.");

            return sb.ToString().TrimEnd();
        }

        static string Summarise(GrassBakeResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[rig-baker] Grass library bake on {result.EngineName}:");
            foreach (var v in result.Variants) sb.AppendLine("  " + v);
            sb.AppendLine(
                $"  {result.Variants.Count} variant(s) rendered in {result.RenderMilliseconds:F0} ms " +
                $"(total {result.TotalMilliseconds:F0} ms), {result.TotalPngBytes / 1024.0:F0} KiB of PNG.");
            sb.Append(
                $"  {result.ManifestPath} rewritten from the rig — {result.ManifestEntries} entries " +
                "(the three shipped tufts included, and NOT re-baked).");
            return sb.ToString();
        }
    }
}
