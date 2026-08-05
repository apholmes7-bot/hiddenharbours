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
    /// <para><b>⭐ THE CLIFF PNGs ARE DELIBERATELY NOT IN THE REPOSITORY.</b> Every sibling kit commits
    /// its sheets; this one measured 16.0 MB for even the narrow St Peters subset (and ~90 MB for the
    /// full parametric set), all of it regenerable in seconds and all of it stale the moment a rig
    /// coefficient moves — so the rig is committed and the pixels are baked locally.</para>
    ///
    /// <para><b>⭐ …WHICH IS WHY THIS MENU IS A CONVENIENCE AND NEVER A REQUIREMENT.</b> The coordinator
    /// made that binding when the rig-not-sheets trade was accepted (PR #427): <i>"a fresh clone
    /// self-heals — a checkout that renders pink until someone finds a menu item is not shipped."</i> So
    /// the region builder calls <see cref="EnsureBaked"/> and bakes on missing, and this menu exists for
    /// re-baking after a rig edit. Both go through <see cref="Bake"/>, so there is exactly one bake path
    /// and the automatic one cannot drift from the one a human has been running.</para>
    /// </summary>
    public static class CliffBakeMenu
    {
        [MenuItem("Hidden Harbours/Dev/Bake Cliff Face Kit (sandstone, 5 aspects × 3 batters)",
                  priority = 146)]
        public static void BakeDefault() => Bake(CliffBaker.DefaultRock);

        [MenuItem("Hidden Harbours/Dev/Bake Cliff Face Kit — till", priority = 147)]
        public static void BakeTill() => Bake("till");

        [MenuItem("Hidden Harbours/Dev/Bake Cliff Face Kit — basalt", priority = 148)]
        public static void BakeBasalt() => Bake("basalt");

        /// <summary>
        /// The one asset whose presence means "this checkout has been baked" — the sandstone south wall's
        /// unlit channel, at the base batter and the base wear step. Chosen rather than a folder
        /// existence check because an EMPTY <c>Faces/</c> folder is exactly what a half-finished bake
        /// leaves behind, and because this is the first face the default bake writes.
        /// </summary>
        public static string SentinelFacePath =>
            $"{CliffBaker.SubFolder(CliffCatalog.BakeRoot, CliffAssetKind.Face)}/" +
            $"{CliffCatalog.FaceName(CliffBaker.DefaultRock, "S", 0, CliffCatalog.BaseStep, CliffChannel)}.png";

        /// <summary>The channel the sentinel is checked on — the one the wall shader cannot render
        /// without.</summary>
        const string CliffChannel = "_unlit";

        /// <summary>True when this checkout already carries baked cliff faces.</summary>
        public static bool IsBaked =>
            AssetDatabase.LoadAssetAtPath<Texture2D>(SentinelFacePath) != null;

        /// <summary>
        /// <b>Bake the kit if — and only if — this checkout has none.</b> The self-heal the fresh-clone
        /// condition rests on: the region builder calls it, so cloning and building gives you a textured
        /// coast without anyone having to know a menu item exists. Returns true if it actually baked.
        ///
        /// <para>Deliberately does NOT re-bake when the sheets are present. A bake is ~30 s and the
        /// builder is re-run constantly; and re-baking on every build would silently overwrite a rock the
        /// owner had chosen from the two alternate menu items above.</para>
        /// </summary>
        public static bool EnsureBaked(string rock = null)
        {
            if (IsBaked) return false;
            Debug.Log($"[cliff-bake] no baked faces at {CliffCatalog.BakeRoot} — baking now " +
                      "(the kit ships as the rig; the PNGs are gitignored and regenerate in seconds).");
            Bake(rock ?? CliffBaker.DefaultRock);
            return true;
        }

        /// <summary>Bake one rock's full St Peters subset, import it, and apply the kit's import
        /// contract. The single bake path — the menu items and <see cref="EnsureBaked"/> both land
        /// here.</summary>
        public static void Bake(string rock)
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
