#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Art
{
    /// <summary>Create the default only after Unity assigns the script and shader their GUIDs.</summary>
    public static class CloudShadowAssets
    {
        [InitializeOnLoadMethod] static void Schedule() => EditorApplication.delayCall += Ensure;

        public static void Ensure()
        {
            const string path = "Assets/_Project/Resources/CloudShadowDef.asset";
            if (AssetDatabase.LoadAssetAtPath<CloudShadowDef>(path) != null) return;
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Project/Art/Shaders/HiddenHarboursCloudShadow.shader");
            if (shader == null) return;
            var def = ScriptableObject.CreateInstance<CloudShadowDef>(); def.Shader = shader;
            AssetDatabase.CreateAsset(def, path); AssetDatabase.SaveAssets();
        }
    }
}
#endif
