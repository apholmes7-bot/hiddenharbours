using System;
using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>Two small recipes own the ground's packed textures as Library artifacts (ADR 0047).</summary>
    [ScriptedImporter(Version, "hhterrain")]
    public sealed class TerrainArrayImporter : ScriptedImporter
    {
        // Increment for packing/decoding/layout semantics, including changes in helper code.
        public const int Version = 1;
        static readonly string[] CodeDependencies =
        {
            "Assets/_Project/Art/Editor/TerrainArrayImporter.cs",
            "Assets/_Project/Art/Editor/TerrainTexArrayBuilder.cs",
            "Assets/_Project/Art/Editor/TerrainArrayAssets.cs",
            "Assets/_Project/Code/Art/TerrainSplatSurface.cs",
        };

        [Serializable] class Recipe { public int schemaVersion; public string kind; }

        public override void OnImportAsset(AssetImportContext ctx)
        {
            foreach (string path in CodeDependencies) ctx.DependsOnSourceAsset(path);
            Object[] output = null;
            try
            {
                Recipe recipe = JsonUtility.FromJson<Recipe>(File.ReadAllText(ctx.assetPath));
                if (recipe == null || recipe.schemaVersion != 1
                    || (recipe.kind != "detail" && recipe.kind != "relight"))
                    throw new InvalidDataException("expected schemaVersion 1 and kind 'detail' or 'relight'.");
                string directory = Path.GetDirectoryName(ctx.assetPath).Replace('\\', '/');
                output = recipe.kind == "detail"
                    ? new Object[] { TerrainTexArrayBuilder.PackDetail(directory, ctx.DependsOnSourceAsset) }
                    : TerrainTexArrayBuilder.PackRelight(directory, ctx.DependsOnSourceAsset);
                // Nothing is published until the whole recipe succeeds. Names are stable identifiers.
                foreach (Object obj in output) ctx.AddObjectToAsset(obj.name, obj);
                ctx.SetMainObject(output[0]);
            }
            catch (Exception e)
            {
                if (output != null)
                    foreach (Object obj in output) if (obj != null) Object.DestroyImmediate(obj);
                ctx.LogImportError("[TerrainArrayImporter] '" + ctx.assetPath + "': " + e.Message);
            }
        }
    }
}
