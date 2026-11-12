using System;
using System.IO;
using System.Linq;
using HiddenHarbours.Art.Editor;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// The next-iteration art pass (2026-11-11): bakes three species from the proposal tree rig
    /// (<c>docs/art/rigs/treeIsoRig5.js</c>) into a folder of their own, beside the kit the game draws.
    /// Same baker, maps, glue and channels as pass 4 (<see cref="TreePass4Baker.WithProposalRig"/>);
    /// its own contract, palette and sheets. No prefab, scene or catalog reads this folder: the trees are
    /// there to be looked at next to the shipped ones and adopted or deleted.
    /// </summary>
    public static class TreeNextVariantBaker
    {
        public const string RigScriptPath = "docs/art/rigs/treeIsoRig5.js";
        public const string RigGlobalName = "TreeRig5";

        /// <summary>The proposal's own folder: never <see cref="TreeKitCatalog.TreesRoot"/>.</summary>
        public const string OutputFolder = "Assets/_Project/Art/Foliage/TreesNext";

        public static string ContractPath => OutputFolder + "/" + TreeKitCatalog.ContractFileName;

        /// <summary>The three commonest species across the two key scenes.</summary>
        public static readonly string[] Species = { "RedMaple", "RedSpruce", "WhiteCedar" };

        /// <summary>Export the proposal's contract, then bake what it declares (summer, mature).</summary>
        public static TreePass4BakeResult Bake()
        {
            using (TreePass4Baker.WithProposalRig(RigScriptPath, RigGlobalName))
            {
                TreePass4Baker.ExportContract(Species, contractPath: ContractPath);
                return TreePass4Baker.Bake(Species, OutputFolder, ContractPath);
            }
        }

        /// <summary>Import-lock and slice every sheet the bake wrote, against the proposal's own contract,
        /// with the kit's own slicer. Returns how many sheets were sliced; throws on the first that fails.</summary>
        public static int Slice()
        {
            AssetDatabase.Refresh();
            string full = Path.Combine(RigCatalog.RepoRoot, ContractPath);
            var contract = JsonUtility.FromJson<TreeKitCatalog.Contract>(File.ReadAllText(full));
            int sliced = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { OutputFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string stem = Path.GetFileNameWithoutExtension(path);
                if (string.Equals(Path.GetFileName(path), TreeKitCatalog.PaletteFileName, StringComparison.Ordinal))
                {
                    TreeSheetSlicer.ApplyPaletteImportSettings(path);
                    continue;
                }
                TreeKitCatalog.Entry entry = contract.trees.FirstOrDefault(
                    e => stem.StartsWith(e.species + "_" + e.stage + "_", StringComparison.Ordinal));
                if (entry == null)
                    throw new InvalidOperationException($"{path} matches no tree in {ContractPath}.");
                if (!TreeSheetSlicer.SliceOne(path, stem, entry))
                    throw new InvalidOperationException($"{path} did not slice; see the log line above.");
                sliced++;
            }
            AssetDatabase.SaveAssets();
            return sliced;
        }

        /// <summary>Headless entry (<c>-executeMethod</c>). Grep the log for the closing marker; a fresh
        /// worktree's first run skips <c>-executeMethod</c>.</summary>
        public static void BakeCli()
        {
            try
            {
                TreePass4BakeResult result = Bake();
                int sliced = Slice();
                foreach (TreeKitCatalog.Entry e in result.Contract.trees)
                    Debug.Log($"[tree-next] {e.species}: cell {e.cellW}x{e.cellH}, sheet {e.sheetW}x{e.sheetH}, " +
                              $"trunk anchor {e.trunkAnchor:F4}");
                Debug.Log($"[tree-next] CLI bake OK — {result.Sheets.Count} sheets, {sliced} sliced, " +
                          $"{result.TotalPngBytes / 1024.0:N0} KB of PNG, " +
                          $"{result.TextureBytes / 1024.0:N0} KB of texture memory, into {OutputFolder}.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[tree-next] CLI bake FAILED: {e}");
                EditorApplication.Exit(1);
            }
        }
    }
}
