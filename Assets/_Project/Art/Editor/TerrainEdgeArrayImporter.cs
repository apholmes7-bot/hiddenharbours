using System;
using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>The five strips' fifteen slices, from PNG bytes, as a Library artifact (ADR 0047).
    /// Source PNGs and their import settings are never rewritten.</summary>
    [ScriptedImporter(1, "hhshorearray")]
    public sealed class TerrainEdgeArrayImporter : ScriptedImporter
    {
        public const string RecipePath = "Assets/_Project/Art/Resources/TerrainShoreLines/TerrainEdge256x128.hhshorearray";
        public const string SourceDirectory = "Assets/_Project/Art/Terrain/Edges";
        public static readonly string[] Kinds = { "Turf", "Scarp", "Wrack", "Weedline", "Reefedge" };
        public static readonly string[] Steps = { "_Lo", "", "_Hi" };
        [Serializable] sealed class Recipe { public int schemaVersion; }

        public override void OnImportAsset(AssetImportContext ctx)
        {
            ctx.DependsOnSourceAsset("Assets/_Project/Art/Editor/TerrainEdgeArrayImporter.cs");
            var array = new Texture2DArray(256, 128, 15, TextureFormat.RGBA32, false, false)
            {
                name = "TerrainEdge256x128", filterMode = FilterMode.Point,
                wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp,
                wrapModeW = TextureWrapMode.Clamp, anisoLevel = 0,
            };
            try
            {
                var recipe = JsonUtility.FromJson<Recipe>(File.ReadAllText(ctx.assetPath));
                if (recipe == null || recipe.schemaVersion != 1) throw new InvalidDataException("Expected shore array schemaVersion 1.");
                foreach (string kind in Kinds)
                foreach (string step in Steps) ctx.DependsOnSourceAsset(SourceDirectory + "/" + kind + step + ".png");
                for (int kind = 0; kind < Kinds.Length; kind++)
                for (int step = 0; step < Steps.Length; step++)
                {
                    string path = SourceDirectory + "/" + Kinds[kind] + Steps[step] + ".png";
                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
                    try
                    {
                        if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(path)) || texture.width != 256 || texture.height != 128)
                            throw new InvalidDataException("Invalid shore strip: " + path);
                        array.SetPixels32(texture.GetPixels32(), kind * 3 + step);
                    }
                    finally { DestroyImmediate(texture); }
                }
                array.Apply(false, false);
                ctx.AddObjectToAsset("TerrainEdge256x128", array); ctx.SetMainObject(array);
            }
            catch (Exception error)
            { DestroyImmediate(array); ctx.LogImportError("[TerrainEdgeArrayImporter] " + error.Message); }
        }
    }
}
