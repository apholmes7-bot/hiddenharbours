using System;
using System.Collections.Generic;
using System.Text;
using HiddenHarbours.Art;
using HiddenHarbours.Player;
using HiddenHarbours.Tools.RigBaking;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>The chest freezer's art, baked and dressed</b> (owner 2026-11-17: detailed appliances with
    /// animations): bakes and slices the freezer sheets (<see cref="ChestFreezerBaker"/>), then builds the
    /// Ginny's freezer prefab: a <see cref="GinnyFreezer"/> with its Def, a <see cref="ChestFreezerArt"/> with a
    /// far and a near renderer, and the <see cref="FreezerContentsPresenter"/> that lays the fish inside.
    ///
    /// <para>Headless: <c>-executeMethod HiddenHarbours.App.Editor.ChestFreezerAssets.BakeAndBuild</c>.</para>
    /// </summary>
    public static class ChestFreezerAssets
    {
        public const string CompletionLine = "[ChestFreezerAssets] DONE";
        public const string PrefabPath = "Assets/_Project/Prefabs/Props/Kitchen/GinnyFreezer.prefab";
        public const string GinnyDefPath = "Assets/_Project/Data/Stowage/GinnyCatchFreezer.asset";

        /// <summary>The Sprite-Lit material every appliance renderer uses.</summary>
        const string SpriteLitMaterialGuid = "a97c105638bdf8b4a8650670310a4cd3";

        /// <summary>Build (or rebuild) the prefab from the sheets already baked.</summary>
        public static void BuildPrefab()
        {
            var def = AssetDatabase.LoadAssetAtPath<FreezerDef>(GinnyDefPath);
            if (def == null) throw new InvalidOperationException($"No FreezerDef at {GinnyDefPath}.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(SpriteLitMaterialGuid));

            var root = new GameObject("GinnyFreezer");
            try
            {
                SpriteRenderer far = NewRenderer(root.transform, "Art", material);
                SpriteRenderer near = NewRenderer(root.transform, "ArtNear", material);
                near.enabled = false;
                far.sprite = ChestFreezerSheet.SpriteAt(def.SheetStem, ChestFreezerSheet.Closed, ChestFreezerSheet.FrontCell);
                near.sprite = ChestFreezerSheet.SpriteAt(def.SheetStem, ChestFreezerSheet.NearOpen, ChestFreezerSheet.FrontCell);
                if (far.sprite == null) throw new InvalidOperationException($"The {def.SheetStem} sheet is not sliced.");

                var freezer = root.AddComponent<GinnyFreezer>();
                var so = new SerializedObject(freezer);
                so.FindProperty("_def").objectReferenceValue = def;
                so.ApplyModifiedPropertiesWithoutUndo();

                var art = root.AddComponent<ChestFreezerArt>();
                so = new SerializedObject(art);
                so.FindProperty("_def").objectReferenceValue = def;
                so.FindProperty("_cell").intValue = ChestFreezerSheet.FrontCell;
                so.FindProperty("_far").objectReferenceValue = far;
                so.FindProperty("_near").objectReferenceValue = near;
                so.ApplyModifiedPropertiesWithoutUndo();

                if (root.GetComponent<FreezerContentsPresenter>() == null) root.AddComponent<FreezerContentsPresenter>();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool ok);
                if (!ok) throw new InvalidOperationException($"Saving {PrefabPath} failed.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            AssetDatabase.SaveAssets();
        }

        static SpriteRenderer NewRenderer(Transform parent, string name, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            if (material != null) sr.sharedMaterial = material;
            return sr;
        }

        [MenuItem("Hidden Harbours/Art/Bake Chest Freezers", priority = 74)]
        public static void BakeAndBuildMenu() => BakeAndBuild();

        /// <summary>Bake, slice, build the prefab; exits in batchmode.</summary>
        public static void BakeAndBuild()
        {
            try
            {
                List<ApplianceSheetBaker.SheetReport> reports = ChestFreezerBaker.BakeAll();
                ChestFreezerBaker.SliceAll(reports);
                BuildPrefab();
                var sb = new StringBuilder(CompletionLine).AppendLine();
                foreach (ApplianceSheetBaker.SheetReport r in reports) sb.AppendLine("  " + r);
                Debug.Log(sb.ToString());
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ChestFreezerAssets] FAILED: {e}");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
        }
    }
}
