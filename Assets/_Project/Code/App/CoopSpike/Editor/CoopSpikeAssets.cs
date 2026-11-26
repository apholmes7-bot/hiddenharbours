using System.IO;
using System.Linq;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;
using HiddenHarbours.Player;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HiddenHarbours.CoopSpike.Editor
{
    public static class CoopSpikeAssets
    {
        public const string Root = "Assets/_Project/Resources/CoopSpike";
        public const string SettingsPath = Root + "/Settings.asset";
        // Ignored by version control and by the asset database (the trailing ~).
        public const string BuildFolder = "Builds~/coop-trial";
        public const string BuildName = "TwoPcSeaTrial.exe";

        /// <summary>The trial application: a development build that offers Host and Join on its title page.</summary>
        public static void BuildWindowsTrial()
        {
            WithTrialProduct(BuildWindowsTrialPlayer);
        }

        // The build carries the trial name; the project and subsequent solo builds retain theirs.
        public static void WithTrialProduct(System.Action build)
        {
            string product = PlayerSettings.productName;
            try
            {
                PlayerSettings.productName = CoopDevStart.TrialProduct;
                build();
            }
            finally { PlayerSettings.productName = product; }
        }

        private static void BuildWindowsTrialPlayer()
        {
            string folder = Path.GetFullPath(BuildFolder);
            string destination = Path.Combine(folder, BuildName);
            Directory.CreateDirectory(folder);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                locationPathName = destination,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development,
                // The flag exists in this build only. Project settings are not touched.
                extraScriptingDefines = new[] { CoopTrialPanel.BuildFlag }
            });
            File.WriteAllText(Path.Combine(folder, "build-summary.txt"),
                $"result={report.summary.result}\nseconds={report.summary.totalTime.TotalSeconds:F3}\nbytes={report.summary.totalSize}\nerrors={report.summary.totalErrors}\nwarnings={report.summary.totalWarnings}\n");
            if (report.summary.result != BuildResult.Succeeded)
                throw new System.InvalidOperationException("The trial build failed.");
        }

        [MenuItem("Tools/Co-op Trial/Build Two Boats Assets")]
        public static void Build()
        {
            Directory.CreateDirectory(Root);
            AssetDatabase.Refresh();
            var settings = AssetDatabase.LoadAssetAtPath<CoopSpikeSettings>(SettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<CoopSpikeSettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
            }
            settings.Roster = StPetersBuilder.DevPickerRosterFiles.Select(name =>
                AssetDatabase.LoadAssetAtPath<BoatHullDef>("Assets/_Project/Data/Boats/" + name + ".asset"))
                .Where(h => h != null).ToArray();
            settings.Character = AssetDatabase.LoadAssetAtPath<CharacterVisualDef>("Assets/_Project/Data/Characters/FisherIso.asset");
            settings.Rod = AssetDatabase.LoadAssetAtPath<ToolDef>("Assets/_Project/Data/Tools/Rod.asset");
            settings.RodSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/_Project/Art/Sprites/Gear/Rod.png");
            var boat = new GameObject("CoopBoat");
            try
            {
                boat.AddComponent<NetworkObject>();
                var movement = boat.AddComponent<NetworkTransform>();
                movement.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
                movement.SyncScaleX = movement.SyncScaleY = movement.SyncScaleZ = false;
                boat.AddComponent<CoopBoatReplica>();
                settings.PlayerPrefab = PrefabUtility.SaveAsPrefabAsset(boat, Root + "/Boat.prefab");
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }
            finally { Object.DestroyImmediate(boat); }
        }
    }
}
