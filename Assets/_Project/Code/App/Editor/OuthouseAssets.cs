using System;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>The outhouse's art, baked and dressed</b>: bakes and slices the outhouse sheet
    /// (<see cref="OuthouseBaker"/>), then rebuilds <c>Prefabs/Washrooms/Outhouse.prefab</c> in place (its GUID,
    /// root and <see cref="SitPoint"/> kept, so <c>washroom.outhouse</c>'s link holds): the placeholder squares
    /// come off, one renderer goes on with an <see cref="OuthouseArt"/> driving the door and casting a
    /// <see cref="SpriteShadow"/> like every standing outdoor piece, and the round placeholder collider
    /// becomes the booth's footprint so he walks round the walls and reaches the door. Places nothing in any scene.
    ///
    /// <para>Headless: <c>-executeMethod HiddenHarbours.App.Editor.OuthouseAssets.BakeAndBuild</c>.</para>
    /// </summary>
    public static class OuthouseAssets
    {
        public const string CompletionLine = "[OuthouseAssets] DONE";
        public const string PrefabPath = InteriorFixturePlacement.OuthousePrefab;
        public const string DefPath = "Assets/_Project/Data/Resources/Washrooms/Outhouse.asset";

        /// <summary>The Sprite-Lit material every appliance renderer uses.</summary>
        const string SpriteLitMaterialGuid = "a97c105638bdf8b4a8650670310a4cd3";

        /// <summary>Rebuild the prefab from the sheet already baked.</summary>
        public static void BuildPrefab()
        {
            var def = AssetDatabase.LoadAssetAtPath<WashroomDef>(DefPath);
            if (def == null) throw new InvalidOperationException($"No WashroomDef at {DefPath}.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(SpriteLitMaterialGuid));
            Sprite shut = OuthouseSheet.SpriteAt(OuthouseSheet.Closed, OuthouseSheet.FrontCell);
            if (shut == null) throw new InvalidOperationException($"The {OuthouseSheet.Stem} sheet is not sliced.");

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                if (root.GetComponent<SitPoint>() == null)
                    throw new InvalidOperationException($"{PrefabPath} has lost its SitPoint; refusing to rebuild it.");

                // Everything under the root was placeholder art (tinted squares); none of it carries logic.
                int sortingLayer = 0;
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                {
                    Transform child = root.transform.GetChild(i);
                    var sr = child.GetComponent<SpriteRenderer>();
                    if (sr != null) sortingLayer = sr.sortingLayerID;
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
                foreach (Collider2D c in root.GetComponents<Collider2D>())
                    if (!(c is PolygonCollider2D)) UnityEngine.Object.DestroyImmediate(c);
                var walls = root.GetComponent<PolygonCollider2D>();
                if (walls == null) walls = root.AddComponent<PolygonCollider2D>();
                walls.isTrigger = false;

                SpriteRenderer booth = NewRenderer(root.transform, "Art", material, sortingLayer);
                booth.sprite = shut;
                booth.gameObject.AddComponent<SpriteShadow>();

                var art = root.GetComponent<OuthouseArt>();
                if (art == null) art = root.AddComponent<OuthouseArt>();
                var so = new SerializedObject(art);
                so.FindProperty("_def").objectReferenceValue = def;
                so.FindProperty("_cell").intValue = OuthouseSheet.FrontCell;
                so.FindProperty("_art").objectReferenceValue = booth;
                so.FindProperty("_walls").objectReferenceValue = walls;
                so.ApplyModifiedPropertiesWithoutUndo();
                walls.pathCount = 1;
                walls.SetPath(0, OuthouseSheet.FootprintOutline(OuthouseSheet.FrontCell));
                booth.sortingOrder = ApplianceArt.OrderFor(0f);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath, out bool ok);
                if (!ok) throw new InvalidOperationException($"Saving {PrefabPath} failed.");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
        }

        static SpriteRenderer NewRenderer(Transform parent, string name, Material material, int sortingLayer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            if (material != null) sr.sharedMaterial = material;
            sr.sortingLayerID = sortingLayer;
            return sr;
        }

        [MenuItem("Hidden Harbours/Art/Bake Outhouse", priority = 75)]
        public static void BakeAndBuildMenu() => BakeAndBuild();

        /// <summary>Bake, slice, rebuild the prefab; exits in batchmode.</summary>
        public static void BakeAndBuild()
        {
            try
            {
                ApplianceSheetBaker.SheetReport report = OuthouseBaker.Bake();
                OuthouseBaker.Slice(report);
                BuildPrefab();
                Debug.Log(CompletionLine + "\n  " + report);
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError($"[OuthouseAssets] FAILED: {e}");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
        }
    }
}
