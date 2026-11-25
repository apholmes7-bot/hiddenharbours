#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
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
        public const string AskId = "ground.stp_north_pocket";
        public const string AskPath = StPetersTerrainPlan.GroundFolder + "/GroundAsk_Fix5_StpNorthPocket.asset";

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

        /// <summary>The source ground with this game's ask omitted; committed maps are outputs.</summary>
        public static GroundFileImport.Result ImportWithoutPocket()
        {
            var plan = UnityEngine.Object.Instantiate(StPetersTerrainPlan.LoadPlan());
            var ground = UnityEngine.Object.Instantiate(plan.Ground);
            try
            {
                ground.Asks = ground.Asks.Where(a => a != null && a.Id != AskId).ToArray();
                plan.Ground = ground;
                return StPetersTerrainPlan.Import(plan);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(ground);
                UnityEngine.Object.DestroyImmediate(plan);
            }
        }

        public static int Apply()
        {
            var ask = AssetDatabase.LoadAssetAtPath<GroundAskDef>(AskPath);
            if (ask == null || ask.Id != AskId || ask.Source != GroundAskSource.Game || ask.Kind != GroundAskKind.Patch)
                throw new InvalidOperationException("The north pocket requires its own game patch ask.");
            if (ask.Retired) return 0;
            var map = StPetersTerrainPlan.LoadSeabed();
            var source = ImportWithoutPocket();
            int width = source.Grid.W, height = source.Grid.H;
            var go = new GameObject("NorthPocketProfile");
            try
            {
                var terrain = go.AddComponent<TidalTerrain>();
                StPetersBuilder.ConfigureTidalTerrain(terrain);
                ushort[] original = TerrainPlanPng.FlipRows(source.Codes, width, height);
                ushort[] updated = PatchCodes(original, width, height, map.WorldCenter,
                    map.WorldSize, map.MinElevation, map.MaxElevation, terrain);
                var samples = new ushort[ask.Width * ask.Height];
                for (int y = 0; y < ask.Height; y++)
                for (int x = 0; x < ask.Width; x++)
                {
                    var at = ask.BoxMin + new Vector2(x * ask.Step, y * ask.Step);
                    int col = Mathf.RoundToInt((at.x - (float)source.Grid.X0) / (float)source.Grid.Mpp - .5f);
                    int row = Mathf.RoundToInt(((float)source.Grid.Y1 - at.y) / (float)source.Grid.Mpp - .5f);
                    if ((uint)col >= width || (uint)row >= height ||
                        Vector2.Distance(TexelCentre(col, row, width, height, map.WorldCenter, map.WorldSize), at) > 1e-5f)
                        throw new InvalidOperationException("A north-pocket patch sample is not a source cell centre.");
                    samples[y * ask.Width + x] = updated[row * width + col];
                }
                ushort[] previous = GroundFileIntake.PatchOf(ask);
                int changed = 0;
                for (int i = 0; i < samples.Length; i++) if (samples[i] != previous[i]) changed++;
                string hash = GroundFileImport.Sha256BigEndian(samples);
                if (changed == 0 && hash == ask.PatchSha256) return 0;
                var pixels = new Color[samples.Length];
                for (int i = 0; i < samples.Length; i++)
                {
                    float value = samples[i] / (float)ushort.MaxValue;
                    pixels[i] = new Color(value, value, value, 1f);
                }
                ask.Patch = PaintedHeightPng.WriteAndImport(AssetDatabase.GetAssetPath(ask.Patch), pixels, ask.Width, ask.Height);
                ask.PatchSha256 = hash;
                EditorUtility.SetDirty(ask);
                AssetDatabase.SaveAssets();
                if (!GroundFileIntake.PatchOf(ask).SequenceEqual(samples))
                    throw new InvalidOperationException("The north-pocket ask samples did not read back exactly.");
                return changed;
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
    }
}
#endif
