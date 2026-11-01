#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    /// <summary>The East Water is a plan and a call to the shared water builder.</summary>
    public static class EastWaterBuilder
    {
        [MenuItem("Hidden Harbours/Build East Water Scene")]
        public static void Build()
        {
            if (!RegionBuildGuard.ConfirmOverwrite("The East Water",
                    WaterSceneTemplate.ScenePathFor(EastWaterPlan.SceneName))) return;
            BuildBatch();
        }

        /// <summary>Non-interactive entry point for -executeMethod. Only builds this region.</summary>
        public static void BuildBatch()
        {
            var camera = WaterSceneTemplate.BuildRegion(
                EastWaterPlan.Plan, EastWaterPlan.DefaultArrivalPos, EastWaterPlan.DefaultArrivalPos,
                new[] { (EastWaterPlan.FromStPetersArrivalKey, EastWaterPlan.FromStPetersArrivalPos) },
                ("PassageToStPeters", EastWaterPlan.ToStPetersPassagePos,
                 WaterSceneTemplate.DataRegions + "/StPeters.asset", StPetersBuilder.FromEastWaterArrivalKey));

            var args = System.Environment.GetCommandLineArgs();
            int review = System.Array.IndexOf(args, "-eastWaterReview");
            if (review >= 0 && review + 1 < args.Length)
                WaterSceneTemplate.CaptureNoonReview(camera, args[review + 1]);
            Debug.Log("[EastWaterBuilder] Built EastWater: 760 by 520 m, St Peters' flat bed, west door and named arrival.");
        }
    }
}
#endif
