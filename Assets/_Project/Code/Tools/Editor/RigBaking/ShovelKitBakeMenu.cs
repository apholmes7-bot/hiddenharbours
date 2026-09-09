using System;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Owner-facing entry point for the clam spade. One click bakes, force-reimports and slices;
    /// nothing further to run. Same philosophy as <see cref="FishingKitBakeMenu"/>, whose
    /// <c>Summarise</c> and post-bake dance this reuses rather than re-writes — the shovel kit
    /// produces a <see cref="FishingBakeResult"/> for exactly that reason.
    /// </summary>
    public static class ShovelKitBakeMenu
    {
        public const string OutputFolder = ShovelKitBaker.DefaultOutputFolder;

        [MenuItem("Hidden Harbours/Art/Bake Shovel Kit (dig 8 dir × 10 frames + 2 rests)",
                  priority = 46)]
        public static void BakeShovelKit()
        {
            FishingBakeResult result;
            try
            {
                result = ShovelKitBaker.Bake(progress: (label, t) =>
                    EditorUtility.DisplayProgressBar("Baking shovel sheets", label, t));
                Debug.Log(FishingKitBakeMenu.Summarise(result));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[rig-baker] shovel kit bake FAILED: {ex.Message}\n{ex}");
                throw;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.Refresh();

            // Reload from disk before anything wires a serialized reference to these sprites — the
            // sheets were written by File.WriteAllBytes behind the AssetDatabase's back.
            foreach (var sheet in result.Sheets)
                AssetDatabase.ImportAsset(sheet.AssetPath, ImportAssetOptions.ForceUpdate);
            if (!string.IsNullOrEmpty(result.AnchorJsonPath))
                AssetDatabase.ImportAsset(result.AnchorJsonPath, ImportAssetOptions.ForceUpdate);

            // Slice in the same operation. ⚠️ Load-bearing rather than a convenience: the slicer
            // walks EVERY png under the fishing Iso root and FAILS on one matching no kit prefix,
            // so a Shovel_* sheet that landed without the manifest entry would redden the slice
            // test rather than import quietly. The prefix is FishingSheetSlicer.Kits["Shovel_"].
            Art.Editor.FishingSheetSlicer.SliceAllMenu();

            Debug.Log("[rig-baker] Shovel kit baked and sliced. COMMIT the results: every " +
                      $"Shovel_*.png + its .meta (LFS covers *.png) under {OutputFolder}/, and " +
                      $"{ShovelKitBaker.MountSidecar} + its .meta.\n" +
                      "⚠️ The CARRIED spade is still unposed — see ShovelKitBaker's remarks: " +
                      "idle/walk/run are ANIM_MOUNT 'free' and tool() returns null on all three, " +
                      "so a shovelTrail PROPS row in characterIsoRig6.hands.js is owed upstream " +
                      "before she can walk the flats with it in her hand.");
        }

        /// <summary>
        /// Headless entry point for CI / -executeMethod.
        ///
        /// ⚠️ Never invoke this with -quit alongside -runTests. The two race: Unity exits 0 having
        /// written total=0, which reads as a pass. Recorded in ADR 0021 and carried here for the
        /// same reason its siblings carry it.
        /// </summary>
        public static void BakeShovelKitFromCommandLine()
        {
            try
            {
                BakeShovelKit();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[rig-baker] headless shovel kit bake failed: {ex}");
                EditorApplication.Exit(1);
            }
        }
    }
}
