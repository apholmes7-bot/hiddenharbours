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
    /// freezer prefabs: Ginny's (a <see cref="GinnyFreezer"/>) and the camper's bigger chest (a
    /// <see cref="CamperFreezer"/>, not yet placed: the camper lane stands it on the lot), each with its Def, a
    /// <see cref="ChestFreezerArt"/> with a far and a near renderer, and the <see cref="FreezerContentsPresenter"/>
    /// that lays the fish inside.
    ///
    /// <para>Headless: <c>-executeMethod HiddenHarbours.App.Editor.ChestFreezerAssets.BakeAndBuild</c>.</para>
    /// </summary>
    public static class ChestFreezerAssets
    {
        public const string CompletionLine = "[ChestFreezerAssets] DONE";
        public const string PrefabPath = "Assets/_Project/Prefabs/Props/Kitchen/GinnyFreezer.prefab";
        public const string GinnyDefPath = "Assets/_Project/Data/Stowage/GinnyCatchFreezer.asset";

        /// <summary>The camper's bigger chest (unplaced: the camper lane stands it on the lot).</summary>
        public const string CamperPrefabPath = "Assets/_Project/Prefabs/Props/Kitchen/CamperFreezer.prefab";
        public const string CamperDefPath = "Assets/_Project/Data/Stowage/CamperCatchFreezer.asset";

        /// <summary>The Sprite-Lit material every appliance renderer uses.</summary>
        const string SpriteLitMaterialGuid = "a97c105638bdf8b4a8650670310a4cd3";

        /// <summary>Build (or rebuild) both freezer prefabs from the sheets already baked.</summary>
        public static void BuildPrefab()
        {
            BuildOne<GinnyFreezer>(GinnyDefPath, PrefabPath, "GinnyFreezer");
            BuildOne<CamperFreezer>(CamperDefPath, CamperPrefabPath, "CamperFreezer");
            AssetDatabase.SaveAssets();
        }

        /// <summary>One chest: the freezer component <typeparamref name="T"/> with its Def, the art with a far
        /// and a near renderer, and the contents presenter.</summary>
        static void BuildOne<T>(string defPath, string prefabPath, string name) where T : MonoBehaviour
        {
            var def = AssetDatabase.LoadAssetAtPath<FreezerDef>(defPath);
            if (def == null) throw new InvalidOperationException($"No FreezerDef at {defPath}.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(SpriteLitMaterialGuid));

            var root = new GameObject(name);
            try
            {
                SpriteRenderer far = NewRenderer(root.transform, "Art", material);
                SpriteRenderer near = NewRenderer(root.transform, "ArtNear", material);
                near.enabled = false;
                far.sprite = ChestFreezerSheet.SpriteAt(def.SheetStem, ChestFreezerSheet.Closed, ChestFreezerSheet.FrontCell);
                near.sprite = ChestFreezerSheet.SpriteAt(def.SheetStem, ChestFreezerSheet.NearOpen, ChestFreezerSheet.FrontCell);
                if (far.sprite == null) throw new InvalidOperationException($"The {def.SheetStem} sheet is not sliced.");

                var freezer = root.AddComponent<T>();
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

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool ok);
                if (!ok) throw new InvalidOperationException($"Saving {prefabPath} failed.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
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
