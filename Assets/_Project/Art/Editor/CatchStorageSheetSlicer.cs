#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// Grid-slicer for the CATCH STORAGE sheets under <c>Assets/_Project/Art/Fishing/Storage/</c>
    /// — the container-fill wave: catch items (<c>CatchItem_&lt;kind&gt;</c>), the insulated tote
    /// (<c>Tote_&lt;colour&gt;_&lt;lid&gt;</c> + <c>ToteMask</c>) and the bucket kit
    /// (<c>Bucket_&lt;tier&gt;_&lt;fill&gt;[_&lt;catch&gt;]</c>). A SIBLING of
    /// <see cref="FishingSheetSlicer"/> with its own root and manifest — that tool's folder is
    /// closed over by its slice tests, and these kits carry four different cells and five
    /// different pivots, none of which belong in its three-entry table — but the slicing ENGINE
    /// (validation, idempotent spriteIDs, row-index naming) is shared via
    /// <see cref="FishingSheetSlicer.SliceSheet"/>, so the rules exist exactly once.
    ///
    /// <para><b>Pivots are the rigs' own, restated as the contract under test</b> (cross-checked
    /// against the live rigs by <c>CatchStorageBakeTests</c>): tote cell 64×72 pivot (32,60) =
    /// ground under the centre (mask identical — it must overlay the tote exactly); bucket cell
    /// 48×52 REST pivot (24,42) = base centre; lobster/crab 64×64 (32,36) = ground centre
    /// (<c>Crustacean.pivot</c>); mussel/clam 14×12 (7,10) = ground contact
    /// (<c>Shellfish.ipivot</c>). Item strips are ONE row (no turntable — CatchKit lays them at
    /// its own scattered angles); container sheets are 8 direction rows.</para>
    /// </summary>
    public static class CatchStorageSheetSlicer
    {
        /// <summary>The only folder this tool slices. We never touch textures outside it.</summary>
        public const string StorageRoot = "Assets/_Project/Art/Fishing/Storage/";

        /// <summary>The kit manifest, by stem prefix. Full-stem prefixes for the item strips
        /// because the two item cells differ; no key is a prefix of another's stems.</summary>
        public static readonly IReadOnlyDictionary<string, FishingSheetSlicer.KitSpec> Kits =
            new Dictionary<string, FishingSheetSlicer.KitSpec>(StringComparer.Ordinal)
            {
                // FishTote: 64×72, pivot (32,60) = ground under the centre; 8 direction rows.
                ["Tote_"] = new FishingSheetSlicer.KitSpec(64, 72, rows: 8, pivotX: 32, pivotY: 60),

                // The opening mask shares the tote's exact cell + pivot so the SpriteMask lands
                // pixel-on-pixel over the container sprite.
                ["ToteMask"] = new FishingSheetSlicer.KitSpec(64, 72, rows: 8, pivotX: 32, pivotY: 60),

                // BucketIso at REST: 48×52, pivotRest (24,42) = base centre; 8 direction rows.
                ["Bucket_"] = new FishingSheetSlicer.KitSpec(48, 52, rows: 8, pivotX: 24, pivotY: 42),

                // Crustacean items: 64×64, pivot (32,36) = ground centre; ONE row × 4 lay variants.
                ["CatchItem_lobster"] = new FishingSheetSlicer.KitSpec(64, 64, rows: 1, pivotX: 32, pivotY: 36),
                ["CatchItem_crab"] = new FishingSheetSlicer.KitSpec(64, 64, rows: 1, pivotX: 32, pivotY: 36),

                // Shellfish items: 14×12, ipivot (7,10) = ground contact; ONE row × 4 lay variants.
                ["CatchItem_mussel"] = new FishingSheetSlicer.KitSpec(14, 12, rows: 1, pivotX: 7, pivotY: 10),
                ["CatchItem_clam"] = new FishingSheetSlicer.KitSpec(14, 12, rows: 1, pivotX: 7, pivotY: 10),
            };

        /// <summary>The kit a stem belongs to, or null for a stranger (which must fail, not
        /// guess).</summary>
        public static FishingSheetSlicer.KitSpec? KitFor(string stem)
        {
            foreach (var kv in Kits)
                if (stem.StartsWith(kv.Key, StringComparison.Ordinal)) return kv.Value;
            return null;
        }

        // ---- entry points -------------------------------------------------------------------

        [MenuItem("Hidden Harbours/Art/Slice Catch Storage Sheets")]
        public static void SliceAllMenu()
        {
            int n = SliceAll(out int failed);
            Debug.Log($"[CatchStorageSheetSlicer] Sliced {n} storage sheet(s) ({failed} failed).");
        }

        /// <summary>Batch entry point for <c>-executeMethod</c> — exits non-zero on any failure
        /// so a headless bake fails loudly instead of committing a half-sliced sheet.</summary>
        public static void SliceAllFromCommandLine()
        {
            try
            {
                AssetDatabase.Refresh();
                int n = SliceAll(out int failed);
                Debug.Log($"[CatchStorageSheetSlicer] (batch) Sliced {n} storage sheet(s) ({failed} failed).");
                if (failed > 0)
                {
                    Debug.LogError($"[CatchStorageSheetSlicer] {failed} sheet(s) failed to slice — see errors above.");
                    EditorApplication.Exit(1);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[CatchStorageSheetSlicer] batch slice threw: {e}");
                EditorApplication.Exit(1);
            }
        }

        // ---- the work -----------------------------------------------------------------------

        public static int SliceAll(out int failed)
        {
            failed = 0;

            if (!Directory.Exists(StorageRoot))
            {
                Debug.LogWarning($"[CatchStorageSheetSlicer] No folder at '{StorageRoot}' — nothing to slice.");
                return 0;
            }

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { StorageRoot.TrimEnd('/') });
            int sliced = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith(StorageRoot, StringComparison.Ordinal)) continue;
                if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;

                string stem = Path.GetFileNameWithoutExtension(path);
                FishingSheetSlicer.KitSpec? kit = KitFor(stem);
                if (kit == null)
                {
                    Debug.LogError($"[CatchStorageSheetSlicer] '{path}' matches no kit prefix " +
                                   $"({string.Join(", ", Kits.Keys)}) — refusing to guess a grid.");
                    failed++;
                    continue;
                }

                if (FishingSheetSlicer.SliceSheet(path, kit.Value)) sliced++;
                else failed++;
            }

            AssetDatabase.SaveAssets();
            return sliced;
        }
    }
}
#endif
