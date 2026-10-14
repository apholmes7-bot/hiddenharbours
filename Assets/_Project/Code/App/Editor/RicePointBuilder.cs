using UnityEditor;

namespace HiddenHarbours.App.Editor
{
    public static class RicePointBuilder
    {
        public const string GroundFolder = "Assets/_Project/Data/Terrain/RicePointGround";
        public const string PlanPath = GroundFolder + "/RicePointPlan.asset";
        public static LandScenePlanDef LoadPlan() => AssetDatabase.LoadAssetAtPath<LandScenePlanDef>(PlanPath);

        [MenuItem("Hidden Harbours/Regions/Rice Point/CREATE Scene")]
        public static void Create()
        {
            GroundFileIntake.ReadRicePointFolder(GroundFolder);
            LandSceneTemplate.Create(LoadPlan());
        }

        [MenuItem("Hidden Harbours/Regions/Rice Point/REFRESH Logic")]
        public static void Refresh()
        {
            GroundFileIntake.ReadRicePointFolder(GroundFolder);
            LandSceneTemplate.Refresh(LoadPlan());
        }
    }
}
