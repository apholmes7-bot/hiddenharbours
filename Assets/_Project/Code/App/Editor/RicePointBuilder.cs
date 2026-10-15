using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

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

        // Batch entry points own scene selection and report success only after the saved output exists.
        public static void CreateCli()
        {
            try
            {
                Create();
                var path = LoadPlan().ScenePath;
                if (!File.Exists(path)) throw new IOException("CREATE did not save its scene.");
                if (!EditorBuildSettings.scenes.Any(s => s.path == path && s.enabled))
                    throw new InvalidOperationException("CREATE did not register its scene.");
                // A saved file is not proof: the marker waits until the scene re-opened from disk holds every
                // door's passage, the region in each list, the dev boat's hull and the fish slots.
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var plan = LoadPlan();
                if (plan.Doors == null || plan.Doors.Length == 0 || plan.Doors.Any(d => d.Target == null))
                    throw new InvalidOperationException("Rice Point needs its doors, each with its target region.");
                Debug.Log("[rice-point] CREATE OK: " + LandSceneTemplate.CheckWiring(plan, scene));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void RefreshCli()
        {
            try
            {
                var scene = EditorSceneManager.OpenScene(LoadPlan().ScenePath, OpenSceneMode.Single);
                Refresh();
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("REFRESH did not save its scene.");
                Debug.Log("[rice-point] REFRESH OK");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }
    }
}
