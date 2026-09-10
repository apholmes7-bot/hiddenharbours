using System;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Owner-facing entry point for the seagull. One click bakes, force-reimports and slices;
    /// nothing further to run. Same philosophy as <see cref="FishingKitBakeMenu"/> and
    /// <see cref="ShovelKitBakeMenu"/>, whose <c>Summarise</c> and post-bake dance this reuses rather
    /// than re-writes — <see cref="SeagullBaker"/> produces a <see cref="FishingBakeResult"/> for
    /// exactly that reason.
    ///
    /// <para>One page, every state: 48 columns in the rig's own <c>sheetOrder()</c> × 8 facing rows
    /// of 64×64 = 3072×512, plus the contract JSON beside it. The gates the bake runs before it
    /// writes a byte (the bill-pixel azimuth probe, the sidecar cross-check, the not-blank and
    /// frames-actually-move controls) live in <see cref="SeagullBaker"/>; this file is the click.</para>
    /// </summary>
    public static class SeagullBakeMenu
    {
        public const string OutputFolder = SeagullBaker.DefaultOutputFolder;

        [MenuItem("Hidden Harbours/Art/Bake Seagull (13 states × 8 dir = 48 columns)", priority = 51)]
        public static void BakeSeagull()
        {
            FishingBakeResult result;
            try
            {
                result = SeagullBaker.Bake(progress: (label, t) =>
                    EditorUtility.DisplayProgressBar("Baking the seagull sheet", label, t));
                Debug.Log(FishingKitBakeMenu.Summarise(result));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[rig-baker] seagull bake FAILED: {ex.Message}\n{ex}");
                throw;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();

            // Reload from disk before anything wires a serialized reference to these sprites — the
            // sheet was written by File.WriteAllBytes behind the AssetDatabase's back.
            foreach (var sheet in result.Sheets)
                AssetDatabase.ImportAsset(sheet.AssetPath, ImportAssetOptions.ForceUpdate);
            if (!string.IsNullOrEmpty(result.AnchorJsonPath))
                AssetDatabase.ImportAsset(result.AnchorJsonPath, ImportAssetOptions.ForceUpdate);

            // Slice in the same operation. ⚠️ Load-bearing rather than a convenience: this sheet is
            // 3072 px wide against Unity's 2048 default maxTextureSize, so an unsliced import is a
            // SILENTLY DOWNSCALED one. The slicer lifts the cap from the contract's
            // requiredMaxTextureSize before it reads the texture; leaving the sheet for a later
            // manual slice is leaving it half-imported.
            Art.Editor.SeagullSheetSlicer.SliceAllMenu();

            Debug.Log("[rig-baker] Seagull baked and sliced. COMMIT the results: " +
                      $"{OutputFolder}/{SeagullBaker.SheetName}.png + its .meta (LFS covers *.png), " +
                      $"and {OutputFolder}/{SeagullBaker.ContractFileName} + its .meta. Stage them BY " +
                      "NAME — a Unity run rewrites boat assets and ProjectSettings, and none of that " +
                      "belongs in this PR.\n" +
                      "⚠️ The four water states (splash, float, preen, peck) carry waterZ:0 in their " +
                      "own pose and are baked WET. A gull preening on a wharf must pass waterZ:null " +
                      "explicitly at runtime or it renders half-submerged on dry land — and the " +
                      "declared transition graph has no dry route into preen or peck at all (float " +
                      "is their only in-edge). Reported upstream, not fixed here.");
        }

        /// <summary>
        /// Headless entry point for CI / -executeMethod.
        ///
        /// ⚠️ Never invoke this with -quit alongside -runTests. The two race: Unity exits 0 having
        /// written total=0, which reads as a pass. Recorded in ADR 0021 and carried here for the
        /// same reason its siblings carry it.
        /// </summary>
        public static void BakeSeagullFromCommandLine()
        {
            try
            {
                BakeSeagull();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[rig-baker] headless seagull bake failed: {ex}");
                EditorApplication.Exit(1);
            }
        }
    }
}
