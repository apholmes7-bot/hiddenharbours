using System;
using System.Linq;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>The only recipe paths and named-object loader. Ordinary loads never trigger a rebuild.</summary>
    public static class TerrainArrayAssets
    {
        public const string DetailRecipePath = TerrainTexArrayBuilder.TexDir + "/TerrainDetail256.hhterrain";
        public const string RelightRecipePath = TerrainTexArrayBuilder.TexDir + "/TerrainRelight.hhterrain";
        public const string DetailName = "TerrainDetail256";
        public const string RampName = "TerrainRelightRamp";
        public static readonly string[] RelightNames =
            { "TerrainRelightNormal", "TerrainRelightLight", "TerrainRelightDetail" };

        public readonly struct RelightSet
        {
            public readonly Texture2DArray Normal, Light, Detail;
            public readonly Texture2D Ramp;
            internal RelightSet(Texture2DArray normal, Texture2DArray light, Texture2DArray detail, Texture2D ramp)
            { Normal = normal; Light = light; Detail = detail; Ramp = ramp; }
        }

        public static Texture2DArray LoadDetailRequired(string recipePath = DetailRecipePath)
        {
            Object[] objects = LoadRequired(recipePath);
            var array = Named<Texture2DArray>(objects, DetailName, recipePath);
            RequireArray(array, true, false, recipePath);
            return array;
        }

        public static RelightSet LoadRelightRequired(string recipePath = RelightRecipePath)
        {
            Object[] objects = LoadRequired(recipePath);
            var arrays = new Texture2DArray[RelightNames.Length];
            for (int a = 0; a < arrays.Length; a++)
            {
                arrays[a] = Named<Texture2DArray>(objects, RelightNames[a], recipePath);
                RequireArray(arrays[a], false, true, recipePath);
            }
            var ramp = Named<Texture2D>(objects, RampName, recipePath);
            if (ramp.width != TerrainSplatSurface.RelightRampWidth || ramp.height != TerrainTexArrayBuilder.Depth
                || ramp.format != TextureFormat.RGBAFloat || ramp.mipmapCount != 1 || !ramp.isReadable
                || ramp.isDataSRGB || ramp.filterMode != FilterMode.Point || ramp.wrapMode != TextureWrapMode.Clamp)
                throw Fault(recipePath, RampName + " has an invalid texture contract.");
            return new RelightSet(arrays[0], arrays[1], arrays[2], ramp);
        }

        static Object[] LoadRequired(string path)
        {
            if (!(AssetImporter.GetAtPath(path) is TerrainArrayImporter))
                throw Fault(path, "recipe has no TerrainArrayImporter.");
            // Unity can retain objects from the last successful import. Never serve them after failure.
            var log = AssetImporter.GetImportLog(path);
            if (log != null && log.logEntries != null)
                foreach (var entry in log.logEntries)
                    if ((entry.flags & ImportLogFlags.Error) != 0)
                        throw Fault(path, entry.message);
            return AssetDatabase.LoadAllAssetsAtPath(path);
        }

        static T Named<T>(Object[] objects, string name, string path) where T : Object
        {
            T[] matches = objects.OfType<T>().Where(obj => obj.name == name).ToArray();
            if (matches.Length != 1) throw Fault(path, "expected exactly one " + typeof(T).Name + " named " + name + ".");
            return matches[0];
        }

        static void RequireArray(Texture2DArray array, bool mips, bool linear, string path)
        {
            int size = TerrainSplatSurface.RelightTileTexels;
            int mipCount = mips ? 1 + (int)Mathf.Log(size, 2) : 1;
            if (array.width != size || array.height != size || array.depth != TerrainTexArrayBuilder.Depth
                || array.format != TextureFormat.RGBA32 || array.mipmapCount != mipCount || !array.isReadable
                || array.isDataSRGB == linear || array.filterMode != FilterMode.Point
                || array.wrapMode != TextureWrapMode.Repeat || array.anisoLevel != 0)
                throw Fault(path, array.name + " has an invalid texture contract.");
        }

        static InvalidOperationException Fault(string path, string why) =>
            new InvalidOperationException("Terrain import '" + path + "' failed: " + why);

        [MenuItem("Hidden Harbours/Art/Reimport Terrain Texture Arrays", priority = 24)]
        public static void ReimportDetail() => AssetDatabase.ImportAsset(DetailRecipePath, ImportAssetOptions.ForceUpdate);

        [MenuItem("Hidden Harbours/Art/Reimport Terrain Relight Arrays", priority = 25)]
        public static void ReimportRelight() => AssetDatabase.ImportAsset(RelightRecipePath, ImportAssetOptions.ForceUpdate);
    }
}
