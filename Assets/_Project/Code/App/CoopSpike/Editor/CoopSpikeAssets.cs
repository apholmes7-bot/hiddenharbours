using System.IO;
using System.Linq;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;
using HiddenHarbours.Player;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.CoopSpike.Editor
{
    public static class CoopSpikeAssets
    {
        public const string Root = "Assets/_Project/Resources/CoopSpike";
        public const string SettingsPath = Root + "/Settings.asset";
        [MenuItem("Hidden Harbours/Spike/Build Two Boats Assets")]
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
