#if UNITY_EDITOR
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    /// <summary>Import entry point: writes only the sea Def and the East Water; never saves St Peters.</summary>
    public static class ArrivalSeaAuthoring
    {
        public const string DefPath = "Assets/_Project/Data/Intro/ArrivalSea.asset";

        public static void Prepare()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Data/Intro"))
                AssetDatabase.CreateFolder("Assets/_Project/Data", "Intro");
            var def = AssetDatabase.LoadAssetAtPath<ArrivalSeaDef>(DefPath);
            if (def == null)
            {
                def = ScriptableObject.CreateInstance<ArrivalSeaDef>();
                AssetDatabase.CreateAsset(def, DefPath);
            }
            def.Id = "intro.arrival_sea";
            def.SeaRegion = AssetDatabase.LoadAssetAtPath<RegionDef>(
                WaterSceneTemplate.RegionAssetPathFor(EastWaterPlan.RegionAssetName));
            def.HomeRegion = AssetDatabase.LoadAssetAtPath<RegionDef>(WaterSceneTemplate.DataRegions + "/StPeters.asset");
            def.OpeningArrivalKey = EastWaterPlan.OpeningArrivalKey;
            def.HomeArrivalKey = StPetersBuilder.FromEastWaterArrivalKey;
            def.LiePosition = EastWaterPlan.OpeningLiePos;
            def.ExitTarget = EastWaterPlan.OpeningExitTarget;
            EditorUtility.SetDirty(def);
            AssetDatabase.SaveAssets();
            EastWaterBuilder.BuildBatch();
        }
    }
}
#endif
