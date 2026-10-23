using System;
using System.IO;
using HiddenHarbours.Core;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Art.Editor
{
    [ScriptedImporter(Version, "hhbathymetry")]
    public sealed class WaveBathymetryImporter : ScriptedImporter
    {
        public const int Version = 1;
        public const string CpuId = "BathymetryCpu", GpuId = "BathymetryGpu";

        public override void OnImportAsset(AssetImportContext ctx)
        {
            WaveBathymetryData cpu = null;
            Texture2DArray gpu = null;
            try
            {
                foreach (string code in WaveBathymetrySource.CodePaths) ctx.DependsOnSourceAsset(code);
                var recipe = JsonUtility.FromJson<WaveBathymetrySource.Recipe>(File.ReadAllText(ctx.assetPath));
                if (recipe == null) throw new InvalidDataException("Empty bathymetry recipe.");
                var input = WaveBathymetrySource.Read(recipe, ctx.assetPath, AssetDatabase.GUIDToAssetPath, ctx.DependsOnSourceAsset);
                var settings = recipe.Settings(); var grid = input.Grid;
                int bins = settings.DirectionX.Length, slices = bins * settings.WaterLevelsMm.Length;
                int sliceBytes = checked(grid.Width * grid.Height * WaveBathymetry.BytesPerCell);
                byte[] bytes = new byte[checked(sliceBytes * slices)];
                for (int level = 0; level < settings.WaterLevelsMm.Length; level++)
                    for (int bin = 0; bin < bins; bin++)
                    {
                        byte[] pixels = WaveBathymetry.Bake(grid, settings, settings.WaterLevelsMm[level], bin).Pixels();
                        Buffer.BlockCopy(pixels, 0, bytes, (level * bins + bin) * sliceBytes, sliceBytes);
                    }
                cpu = ScriptableObject.CreateInstance<WaveBathymetryData>();
                cpu.name = CpuId; cpu.Id = recipe.id; cpu.FormatVersion = Version;
                cpu.Width = grid.Width; cpu.Height = grid.Height; cpu.CellSizeMm = settings.CellSizeMm;
                cpu.MinXmm = grid.MinXmm; cpu.MinYmm = grid.MinYmm;
                cpu.DirectionX = settings.DirectionX; cpu.DirectionY = settings.DirectionY;
                cpu.EntryDepthsMm = new int[settings.WaterLevelsMm.Length];
                for (int i = 0; i < cpu.EntryDepthsMm.Length; i++)
                    cpu.EntryDepthsMm[i] = WaveBathymetry.EntryDepthMm(grid, settings, settings.WaterLevelsMm[i]);
                cpu.WaterLevelsMm = settings.WaterLevelsMm; cpu.Pixels = bytes;
                gpu = new Texture2DArray(grid.Width, grid.Height, slices, TextureFormat.RGBAFloat, false, true)
                {
                    name = GpuId, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, anisoLevel = 0,
                };
                for (int s = 0; s < slices; s++)
                {
                    gpu.SetPixelData(bytes, 0, s, s * sliceBytes);
                    var packed = gpu.GetPixelData<byte>(0, s);
                    for (int i = 0; i < sliceBytes; i++)
                        if (packed[i] != bytes[s * sliceBytes + i])
                            throw new InvalidDataException("GPU staging differs at slice " + s + ", byte " + i + ".");
                }
                gpu.Apply(false, true); // CPU payload above is the only retained readable copy.
                // Publish only after every slice and both objects succeed.
                ctx.AddObjectToAsset(CpuId, cpu); ctx.AddObjectToAsset(GpuId, gpu); ctx.SetMainObject(cpu);
            }
            catch (Exception e)
            {
                if (cpu != null) Object.DestroyImmediate(cpu);
                if (gpu != null) Object.DestroyImmediate(gpu);
                ctx.LogImportError("[WaveBathymetryImporter] '" + ctx.assetPath + "': " + e.Message);
            }
        }
    }
}
