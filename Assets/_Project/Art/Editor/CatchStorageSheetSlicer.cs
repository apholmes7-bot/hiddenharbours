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

                // ---- catch pass 2 -------------------------------------------------------------
                //
                // Every pass-2 stem carries a 2, so the pass-1 entries above keep working until
                // their consumers switch. No key here is a prefix of another's stems: "Crust2_" and
                // "Crust2Held_" diverge at the underscore, and so do "Shell2_" and "Shell2Hand_".

                // Crustacean2 on the ground: 64×64, pivot (32,40) = ground centre; 8 direction rows.
                ["Crust2_"] = new FishingSheetSlicer.KitSpec(64, 64, rows: 8, pivotX: 32, pivotY: 40),

                // ⚠️ HELD is its own stem because it is its own PIVOT: hpivot (32,12), the grip on
                // the animal's back. Measured — a held cell's content sits at y≈14 while a walking
                // one sits at y≈38, so slicing held on the ground pivot would hang it in mid-air.
                ["Crust2Held_"] = new FishingSheetSlicer.KitSpec(64, 64, rows: 8, pivotX: 32, pivotY: 12),

                // Shellfish2 at STRICT WORLD SCALE — 8×8, not pass 1's 14×12, because scale 1 is now
                // the real animal (a periwinkle is ONE PIXEL). Loose shell pivots on ground contact.
                ["Shell2_"] = new FishingSheetSlicer.KitSpec(8, 8, rows: 1, pivotX: 4, pivotY: 6),

                // The handful pivots on THE GRIP (4,4) — one clutch per hand.
                ["Shell2Hand_"] = new FishingSheetSlicer.KitSpec(8, 8, rows: 1, pivotX: 4, pivotY: 4),

                // The clam hod: 40×40, pivot (20,30) = ground centre; 8 direction rows, one frame.
                // Two stems (back/front) because the basket is hollow — the heap goes between them.
                ["Hod2_"] = new FishingSheetSlicer.KitSpec(40, 40, rows: 8, pivotX: 20, pivotY: 30),

                // Pass-2 composed items. The crustaceans keep the 64×64 cell but move to
                // Crustacean2's (32,40) pivot — pass 1's Crustacean pivoted at (32,36), so these are
                // NOT the same spec as the CatchItem_ entries above and must not inherit them.
                ["CatchItem2_lobster"] = new FishingSheetSlicer.KitSpec(64, 64, rows: 1, pivotX: 32, pivotY: 40),
                ["CatchItem2_crab"] = new FishingSheetSlicer.KitSpec(64, 64, rows: 1, pivotX: 32, pivotY: 40),

                ["CatchItem2_mussel"] = new FishingSheetSlicer.KitSpec(8, 8, rows: 1, pivotX: 4, pivotY: 6),
                ["CatchItem2_clam"] = new FishingSheetSlicer.KitSpec(8, 8, rows: 1, pivotX: 4, pivotY: 6),
                ["CatchItem2_scallop"] = new FishingSheetSlicer.KitSpec(8, 8, rows: 1, pivotX: 4, pivotY: 6),
                ["CatchItem2_oyster"] = new FishingSheetSlicer.KitSpec(8, 8, rows: 1, pivotX: 4, pivotY: 6),
                ["CatchItem2_periwinkle"] = new FishingSheetSlicer.KitSpec(8, 8, rows: 1, pivotX: 4, pivotY: 6),
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

        [MenuItem("Hidden Harbours/Art/Import (after a new drop)/Slice Catch Storage Sheets", priority = 205)]
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
