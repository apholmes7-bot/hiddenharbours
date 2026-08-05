using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// The owner-facing entry point for the cliff kit: one menu item, ~30 s, and the wall textures
    /// exist.
    ///
    /// <para><b>⭐ THIS IS NOT AN OPTIONAL STEP — the cliff PNGs are deliberately not in the
    /// repository.</b> Every sibling kit commits its sheets; this one measured 16.0 MB for even the
    /// narrow St Peters subset (and ~90 MB for the full parametric set), all of it regenerable in
    /// seconds and all of it stale the moment a rig coefficient moves — so the rig is committed and the
    /// pixels are baked locally. A fresh clone therefore renders cliff geometry with no cliff texture
    /// until this runs. That is by design; see <see cref="CliffCatalog"/>.</para>
    /// </summary>
    public static class CliffBakeMenu
    {
        [MenuItem("Hidden Harbours/Dev/Bake Cliff Face Kit (sandstone, 5 aspects × 3 batters)",
                  priority = 146)]
        public static void BakeDefault() => Run(CliffBaker.DefaultRock);

        [MenuItem("Hidden Harbours/Dev/Bake Cliff Face Kit — till", priority = 147)]
        public static void BakeTill() => Run("till");

        [MenuItem("Hidden Harbours/Dev/Bake Cliff Face Kit — basalt", priority = 148)]
        public static void BakeBasalt() => Run("basalt");

        static void Run(string rock)
        {
            CliffBakeResult result;
            try
            {
                result = CliffBaker.BakeAll(
                    rock: rock,
                    progress: (what, t) => EditorUtility.DisplayProgressBar(
                        $"Baking the cliff kit ({rock})", what, t));
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[cliff-bake] {rock}: {e.Message}");
                throw;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            // Import BEFORE the settings pass — an asset Unity has not seen yet has no importer to
            // configure, which is the quiet way a kit ends up point-filtered-by-default and sRGB-on.
            AssetDatabase.Refresh();

            int retuned = 0;
            foreach (CliffAssetBake a in result.Assets)
            {
                string channel = CliffCatalog.LiveChannels.FirstOrDefault(c => a.Name.EndsWith(c)) ?? "";
                if (CliffBaker.ApplyImportSettings(a.AssetPath, a.Kind, channel)) retuned++;
            }

            Debug.Log(
                $"[cliff-bake] {rock} via {result.EngineName}: " +
                $"{result.FaceCount} face sets ({result.FaceCount * CliffCatalog.LiveChannels.Length} " +
                $"channel PNGs), {result.StripCount} brow/toe decals, {result.LedgeCount} ledge sheets, " +
                $"{result.ProfileCount} profiles — {result.TotalPngBytes / 1048576.0:F1} MB total, " +
                $"{retuned} import settings applied. " +
                $"Render {result.RenderMilliseconds / 1000.0:F1} s of " +
                $"{result.TotalMilliseconds / 1000.0:F1} s.\n" +
                $"Written under {CliffCatalog.BakeRoot} (gitignored — the rig is the committed source).");
        }
    }
}
