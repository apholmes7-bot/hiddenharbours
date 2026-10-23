using System;
using System.Linq;
using HiddenHarbours.Core;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>Required editor load, without reimport side effects. Rejects retained failed artifacts.</summary>
    public static class WaveBathymetryAssets
    {
        public const string Directory = "Assets/_Project/Art/Terrain/Bathymetry";
        public static WaveBathymetryData LoadRequired(string path, out Texture2DArray gpu)
        {
            if (!(AssetImporter.GetAtPath(path) is WaveBathymetryImporter))
                throw new InvalidOperationException("'" + path + "': bathymetry importer missing.");
            var log = AssetImporter.GetImportLog(path);
            if (log != null && log.logEntries != null)
                foreach (var entry in log.logEntries)
                    if ((entry.flags & ImportLogFlags.Error) != 0)
                        throw new InvalidOperationException("'" + path + "': " + entry.message);
            var objects = AssetDatabase.LoadAllAssetsAtPath(path);
            var cpu = objects.OfType<WaveBathymetryData>().SingleOrDefault();
            gpu = objects.OfType<Texture2DArray>().SingleOrDefault();
            if (cpu == null || gpu == null || cpu.FormatVersion != WaveBathymetryImporter.Version
                || cpu.Width < 2 || cpu.Height < 2 || cpu.DirectionX == null || cpu.WaterLevelsMm == null
                || cpu.DirectionY == null || cpu.DirectionX.Length != cpu.DirectionY.Length
                || cpu.EntryDepthsMm == null || cpu.EntryDepthsMm.Length != cpu.WaterLevelsMm.Length
                || gpu.width != cpu.Width || gpu.height != cpu.Height
                || gpu.depth != cpu.DirectionX.Length * cpu.WaterLevelsMm.Length
                || gpu.format != TextureFormat.RGBAFloat || gpu.mipmapCount != 1 || gpu.isDataSRGB || gpu.isReadable
                || gpu.filterMode != FilterMode.Bilinear || gpu.wrapMode != TextureWrapMode.Clamp
                || cpu.Pixels == null || cpu.Pixels.Length != cpu.Width * cpu.Height * gpu.depth * WaveBathymetry.BytesPerCell)
                throw new InvalidOperationException("'" + path + "': invalid bathymetry objects.");
            return cpu;
        }
        [MenuItem("Hidden Harbours/Art/Reimport Wave Bathymetry", priority = 26)]
        public static void Reimport()
        {
            foreach (string guid in AssetDatabase.FindAssets("", new[] { Directory }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".hhbathymetry", StringComparison.Ordinal))
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
        }
    }
}
