#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    public static class ModernTruckBakeCli
    {
        public static void Bake()
        {
            try
            {
                foreach (string key in new[] { "modern350", "modern2500" })
                {
                    var mesh = VehicleMeshAssetBaker.Bake(VehicleRigFleet.Get(key));
                    Debug.Log($"[modern-trucks] {key}: {mesh.Mesh.vertexCount} body vertices, {mesh.Wheels.Length} fittings.");
                }
                AssetDatabase.SaveAssets();
                Debug.Log("[modern-trucks] Both trucks baked.");
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                EditorApplication.Exit(1);
            }
        }
    }
}
#endif
