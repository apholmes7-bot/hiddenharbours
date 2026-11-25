#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    /// <summary>The South Water is a plan and a call to the shared water builder.</summary>
    public static class SouthWaterBuilder
    {
        [MenuItem("Hidden Harbours/Build South Water Scene")]
        public static void Build()
        {
            if (!RegionBuildGuard.ConfirmOverwrite("The South Water",
                    WaterSceneTemplate.ScenePathFor(SouthWaterPlan.SceneName))) return;
            BuildBatch();
        }

        /// <summary>Non-interactive entry point for -executeMethod. Only builds this region.</summary>
        public static void BuildBatch()
        {
            var camera = WaterSceneTemplate.BuildRegion(
                SouthWaterPlan.Plan, SouthWaterPlan.DefaultArrivalPos, SouthWaterPlan.DefaultArrivalPos,
                SouthWaterPlan.NamedArrivals, SouthWaterPlan.PassageBandSize, SouthWaterPlan.Passages);

            var args = System.Environment.GetCommandLineArgs();
            int review = System.Array.IndexOf(args, "-southWaterReview");
            if (review >= 0 && review + 1 < args.Length)
                WaterSceneTemplate.CaptureNoonReview(camera, args[review + 1]);
            Debug.Log($"[SouthWaterBuilder] Built SouthWater: {SouthWaterPlan.RegionWorldSize.x:0} by " +
                      $"{SouthWaterPlan.RegionWorldSize.y:0} m, shelf {SouthWaterPlan.ShelfElevation:0} m, " +
                      $"deep floor {SouthWaterPlan.DeepFloorElevation:0} m, bank {SouthWaterPlan.BankElevation:0} m.");
        }
    }
}
#endif
