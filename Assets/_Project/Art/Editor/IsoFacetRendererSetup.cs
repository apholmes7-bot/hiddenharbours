using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// Installs <see cref="IsoFacetHullFeature"/> onto the project's 2D renderer asset
    /// (ADR 0022 phase 3). Renderer features live as SUB-ASSETS of the renderer data plus two
    /// serialized lists (<c>m_RendererFeatures</c> and the 64-bit <c>m_RendererFeatureMap</c>),
    /// which is why this is an editor utility and not a hand YAML edit. Idempotent: running it
    /// again on an already-installed renderer changes nothing.
    /// </summary>
    public static class IsoFacetRendererSetup
    {
        private const string RendererAssetPath = "Assets/Settings/Renderer2D.asset";
        private const string ResolveShaderPath =
            "Assets/_Project/Art/Shaders/HiddenHarboursIsoFacetResolve.shader";

        [MenuItem("Hidden Harbours/Setup/Install IsoFacet Hull Feature")]
        public static void Install()
        {
            var data = AssetDatabase.LoadAssetAtPath<Renderer2DData>(RendererAssetPath);
            if (data == null)
                throw new InvalidOperationException($"No Renderer2DData at {RendererAssetPath}.");

            var so = new SerializedObject(data);
            var features = so.FindProperty("m_RendererFeatures");
            var featureMap = so.FindProperty("m_RendererFeatureMap");
            if (features == null || featureMap == null)
                throw new InvalidOperationException(
                    "Renderer2DData no longer serializes m_RendererFeatures/m_RendererFeatureMap — " +
                    "URP changed shape; update this utility.");

            for (int i = 0; i < features.arraySize; i++)
            {
                if (features.GetArrayElementAtIndex(i).objectReferenceValue is IsoFacetHullFeature)
                {
                    Debug.Log("[IsoFacetRendererSetup] Already installed; nothing to do.");
                    return;
                }
            }

            var feature = ScriptableObject.CreateInstance<IsoFacetHullFeature>();
            feature.name = nameof(IsoFacetHullFeature);

            // Pin the resolve shader by reference so it survives into builds (Shader.Find alone
            // would be stripped from a player that never references it from a scene).
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ResolveShaderPath);
            if (shader == null)
                throw new InvalidOperationException($"Resolve shader missing at {ResolveShaderPath}.");
            var featureSo = new SerializedObject(feature);
            featureSo.FindProperty("_resolveShader").objectReferenceValue = shader;
            featureSo.ApplyModifiedPropertiesWithoutUndo();

            AssetDatabase.AddObjectToAsset(feature, data);
            // The local file id only exists once the sub-asset has been written to disk.
            AssetDatabase.SaveAssets();
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId) || localId == 0)
                throw new InvalidOperationException("Could not resolve the new feature's local file id.");

            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
            featureMap.arraySize++;
            featureMap.GetArrayElementAtIndex(featureMap.arraySize - 1).longValue = localId;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Debug.Log("[IsoFacetRendererSetup] IsoFacetHullFeature installed on Renderer2D.asset.");
        }
    }
}
