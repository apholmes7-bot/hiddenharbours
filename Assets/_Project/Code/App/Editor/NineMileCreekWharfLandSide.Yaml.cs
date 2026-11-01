#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;

namespace HiddenHarbours.App.Editor
{
    public static partial class NineMileCreekWharfLandSide
    {
        const int GameObjectClass = 1, TransformClass = 4, SpriteRendererClass = 212, MonoBehaviourClass = 114, PolygonCollider2DClass = 60;
        const string KeyScenesStep = PlacementStep;
        static string Id(long value) => value.ToString(CultureInfo.InvariantCulture);
        static string Int(int value) => value.ToString(CultureInfo.InvariantCulture);
        static string F(float value) => value == 0f ? "0" : value.ToString("G9", CultureInfo.InvariantCulture);
        static string Plain(string value) => value;
        static string Header(int kind, long id, string type) => "--- !u!" + kind + " &" + Id(id) + "\n" + type + ":";
        static string Vec2Yaml(Vector2 value) => "{x: " + F(value.x) + ", y: " + F(value.y) + "}";
        static string Vec3Yaml(float x, float y, float z) => "{x: " + F(x) + ", y: " + F(y) + ", z: " + F(z) + "}";
        static string SetField(string text, string key, string value)
        {
            var rx = new Regex(@"(?m)^  " + Regex.Escape(key) + @":.*$");
            if (rx.Matches(text).Count != 1) throw new Refusal(PlacementStep + ": missing unique field " + key);
            return rx.Replace(text, _ => "  " + key + ": " + value);
        }
        const string Unlinked = "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n" +
                                "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}";

        static string GameObjectYaml(long id, string name, IList<long> components)
        {
            var lines = new List<string> { Header(GameObjectClass, id, "GameObject"), Unlinked, "  serializedVersion: 6", "  m_Component:" };
            foreach (long c in components) lines.Add($"  - component: {{fileID: {Id(c)}}}");
            lines.Add("  m_Layer: 0");
            lines.Add("  m_Name: " + Plain(name));
            lines.Add("  m_TagString: Untagged");
            lines.Add("  m_Icon: {fileID: 0}");
            lines.Add("  m_NavMeshLayer: 0");
            lines.Add("  m_StaticEditorFlags: 0");
            lines.Add("  m_IsActive: 1");
            return string.Join("\n", lines);
        }

        static string TransformYaml(long id, long go, Vector2 at, IList<long> children, long father)
        {
            var lines = new List<string>
            {
                Header(TransformClass, id, "Transform"), Unlinked,
                $"  m_GameObject: {{fileID: {Id(go)}}}",
                "  serializedVersion: 2",
                "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}",
                "  m_LocalPosition: " + Vec3Yaml(at.x, at.y, 0f),
                "  m_LocalScale: {x: 1, y: 1, z: 1}",
                "  m_ConstrainProportionsScale: 0",
                children.Count == 0 ? "  m_Children: []" : "  m_Children:",
            };
            foreach (long c in children) lines.Add($"  - {{fileID: {Id(c)}}}");
            lines.Add($"  m_Father: {{fileID: {Id(father)}}}");
            lines.Add("  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}");
            return string.Join("\n", lines);
        }

        static string SpriteRendererYaml(long id, long go, ObjRef material, SpriteRef sprite, int order) => string.Join("\n",
            $"--- !u!{Int(SpriteRendererClass)} &{Id(id)}", "SpriteRenderer:", "  serializedVersion: 3", Unlinked,
            $"  m_GameObject: {{fileID: {Id(go)}}}",
            "  m_Enabled: 1", "  m_CastShadows: 0", "  m_ReceiveShadows: 0", "  m_DynamicOccludee: 1", "  m_StaticShadowCaster: 0",
            "  m_MotionVectors: 1", "  m_LightProbeUsage: 1", "  m_ReflectionProbeUsage: 1", "  m_RayTracingMode: 0",
            "  m_RayTraceProcedural: 0", "  m_RayTracingAccelStructBuildFlagsOverride: 0", "  m_RayTracingAccelStructBuildFlags: 1",
            "  m_SmallMeshCulling: 1", "  m_ForceMeshLod: -1", "  m_MeshLodSelectionBias: 0", "  m_RenderingLayerMask: 1",
            "  m_RendererPriority: 0", "  m_Materials:", "  - " + material.Yaml, "  m_StaticBatchInfo:", "    firstSubMesh: 0",
            "    subMeshCount: 0", "  m_StaticBatchRoot: {fileID: 0}", "  m_ProbeAnchor: {fileID: 0}",
            "  m_LightProbeVolumeOverride: {fileID: 0}", "  m_ScaleInLightmap: 1", "  m_ReceiveGI: 1", "  m_PreserveUVs: 0",
            "  m_IgnoreNormalsForChartDetection: 0", "  m_ImportantGI: 0", "  m_StitchLightmapSeams: 1",
            "  m_SelectedEditorRenderState: 0", "  m_MinimumChartSize: 4", "  m_AutoUVMaxDistance: 0.5", "  m_AutoUVMaxAngle: 89",
            "  m_LightmapParameters: {fileID: 0}", "  m_GlobalIlluminationMeshLod: 0", "  m_SortingLayerID: 0", "  m_SortingLayer: 0",
            "  m_SortingOrder: " + Int(order), "  m_MaskInteraction: 0", "  m_Sprite: " + sprite.Sprite.Yaml,
            "  m_Color: {r: 1, g: 1, b: 1, a: 1}", "  m_FlipX: 0", "  m_FlipY: 0", "  m_DrawMode: 0", "  m_Size: " + Vec2Yaml(sprite.Size),
            "  m_AdaptiveModeThreshold: 0.5", "  m_SpriteTileMode: 0", "  m_WasSpriteAssigned: 1", "  m_SpriteSortPoint: 0",
            "  m_BlendShapeWeights: []");

        static string MonoBehaviourYaml(long id, long go, ScriptRef script, IEnumerable<string> fields)
        {
            if (string.IsNullOrEmpty(script.Script.Guid) || string.IsNullOrEmpty(script.ClassIdentifier))
                throw new Refusal($"{KeyScenesStep}: a component with no script.");
            var lines = new List<string>
            {
                Header(MonoBehaviourClass, id, "MonoBehaviour"), Unlinked,
                $"  m_GameObject: {{fileID: {Id(go)}}}",
                "  m_Enabled: 1",
                "  m_EditorHideFlags: 0",
                "  m_Script: " + script.Script.Yaml,
                "  m_Name: ",
                "  m_EditorClassIdentifier: " + Plain(script.ClassIdentifier),
            };
            lines.AddRange(fields);
            return string.Join("\n", lines);
        }

        static string PolygonColliderYaml(long id, long go, IList<Vector2[]> walls)
        {
            var lines = new List<string>
            {
                Header(PolygonCollider2DClass, id, "PolygonCollider2D"), Unlinked,
                $"  m_GameObject: {{fileID: {Id(go)}}}",
                "  m_Enabled: 1", "  serializedVersion: 3", "  m_Density: 1", "  m_Material: {fileID: 0}",
                "  m_IncludeLayers:", "    serializedVersion: 2", "    m_Bits: 0",
                "  m_ExcludeLayers:", "    serializedVersion: 2", "    m_Bits: 0",
                "  m_LayerOverridePriority: 0",
                "  m_ForceSendLayers:", "    serializedVersion: 2", "    m_Bits: 4294967295",
                "  m_ForceReceiveLayers:", "    serializedVersion: 2", "    m_Bits: 4294967295",
                "  m_ContactCaptureLayers:", "    serializedVersion: 2", "    m_Bits: 4294967295",
                "  m_CallbackLayers:", "    serializedVersion: 2", "    m_Bits: 4294967295",
                "  m_IsTrigger: 0", "  m_UsedByEffector: 0", "  m_CompositeOperation: 0", "  m_CompositeOrder: 0",
                "  m_Offset: {x: 0, y: 0}",
                "  m_SpriteTilingProperty:", "    border: {x: 0, y: 0, z: 0, w: 0}", "    pivot: {x: 0, y: 0}",
                "    oldSize: {x: 0, y: 0}", "    newSize: {x: 0, y: 0}", "    adaptiveTilingThreshold: 0", "    drawMode: 0",
                "    adaptiveTiling: 0",
                "  m_AutoTiling: 0",
                "  m_Points:",
                "    m_Paths:",
            };
            foreach (Vector2[] wall in walls)
                for (int i = 0; i < wall.Length; i++)
                    lines.Add((i == 0 ? "    - - " : "      - ") + Vec2Yaml(wall[i]));
            lines.Add("  m_UseDelaunayMesh: 1");
            return string.Join("\n", lines);
        }

    }
}
#endif
