#if UNITY_EDITOR
using System;
using System.IO;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    /// <summary>A bounded, repeatable change to the existing painted north-berth depth.</summary>
    public static class StPetersNorthPocket
    {
        public const string MapPath = "Assets/_Project/Data/Terrain/StPetersSeabed.asset";
        public const string PngPath = "Assets/_Project/Data/Terrain/StPetersSeabed_HeightTex.png";

        // File row zero is north. The height field samples texel centres, never pixel corners.
        public static Vector2 TexelCentre(int x, int row, int width, int height, Vector2 centre, Vector2 size) =>
            centre - size * 0.5f + new Vector2((x + 0.5f) * size.x / width,
                                               (height - row - 0.5f) * size.y / height);

        public static ushort[] PatchCodes(ushort[] original, int width, int height,
            Vector2 centre, Vector2 size, float minElevation, float maxElevation, TidalTerrain terrain)
        {
            if (original.Length != width * height || maxElevation <= minElevation)
                throw new ArgumentException("North-pocket height dimensions or elevation range are invalid.");
            var result = (ushort[])original.Clone();
            Rect bounds = StPetersBuilder.NorthPocketRectangle;
            for (int row = 0; row < height; row++)
            for (int x = 0; x < width; x++)
            {
                Vector2 at = TexelCentre(x, row, width, height, centre, size);
                if (!bounds.Contains(at)) continue;
                float before = terrain.ElevationAtWithoutNorthPocket(at);
                float after = terrain.NorthPocketElevation(at, before);
                if (after >= before) continue;
                ushort target = (ushort)Mathf.RoundToInt(Mathf.Clamp01(
                    (after - minElevation) / (maxElevation - minElevation)) * ushort.MaxValue);
                int i = row * width + x;
                // Recompute from the uncut profile, then only lower. Applying the shoulder twice
                // must not progressively deepen it, and measured painted detail is never raised.
                result[i] = Math.Min(original[i], target);
            }
            return result;
        }

        public static int Apply()
        {
            var map = AssetDatabase.LoadAssetAtPath<PaintedHeightMap>(MapPath);
            if (map == null) throw new InvalidOperationException("The existing painted seabed map is absent.");
            var image = TerrainPlanPng.Read(File.ReadAllBytes(PngPath));
            if (image.BitDepth != 16 || image.ColourType != 0)
                throw new InvalidOperationException("The north pocket requires the existing R16 greyscale map.");
            var go = new GameObject("NorthPocketProfile");
            try
            {
                var terrain = go.AddComponent<TidalTerrain>();
                StPetersBuilder.ConfigureTidalTerrain(terrain);
                ushort[] original = image.Channel(0);
                ushort[] updated = PatchCodes(original, image.Width, image.Height, map.WorldCenter,
                    map.WorldSize, map.MinElevation, map.MaxElevation, terrain);
                int changed = 0;
                for (int i = 0; i < original.Length; i++) if (updated[i] != original[i]) changed++;
                if (changed == 0) return 0;
                var pixels = new Color[updated.Length];
                for (int row = 0; row < image.Height; row++)
                for (int x = 0; x < image.Width; x++)
                {
                    float value = updated[row * image.Width + x] / (float)ushort.MaxValue;
                    pixels[(image.Height - row - 1) * image.Width + x] = new Color(value, value, value, 1f);
                }
                PaintedHeightPng.WriteAndImport(PngPath, pixels, image.Width, image.Height);
                return changed;
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
#endif
