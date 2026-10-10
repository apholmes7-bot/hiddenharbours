#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    public sealed class PlantPlateGround : MonoBehaviour, IPlantFieldGround
    {
        public float Tide;
        public PlantGroundSample Sample(float x, float y) => new PlantGroundSample(0, 0, 0);
        public float WaterLevelAt(float x, float y) => Tide;
        public int PlantSeason => 1;
    }

    public sealed class PlantRig5PlatePlayTests
    {
        const string ArtPath = "Assets/_Project/Art/Foliage/Plants5";
        const string DefPath = "Assets/_Project/Data/Plants5";
        const string OutPath = "artifacts/stp-plants/plate";
        static readonly float[] Hours = { 6f, 12f, 14.5167f, 15.5f, 17.733f };
        static readonly float[] Tides = { -.85f, .63f, 2.18f, 0f, -.1f };

        [Serializable] public sealed class MapRow
        {
            public string path, format, type;
            public int width, height, sprites;
            public bool readable, srgb;
            public long bytes;
        }
        [Serializable] public sealed class MemoryReport
        {
            public bool playing;
            public string unity, device;
            public long bytes, beforeDrawBytes;
            public MapRow[] maps;
        }
        [Serializable] public sealed class CellRow
        {
            public string key, file;
            public int timeIndex, tideIndex, width, height, opaque;
            public float hour, tide;
        }
        [Serializable] public sealed class PlateReport { public CellRow[] cells; }

        [UnityTest]
        public IEnumerator AllSpeciesAtBoardHoursAndLoadedTextureMemory()
        {
            Assert.That(Application.isPlaying, Is.True);
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null));
            Directory.CreateDirectory(OutPath);
            var defs = AssetDatabase.FindAssets("t:PlantSpeciesDef", new[] { DefPath })
                .Select(g => AssetDatabase.LoadAssetAtPath<PlantSpeciesDef>(AssetDatabase.GUIDToAssetPath(g)))
                .OrderBy(d => d.Code).ToArray();
            Assert.That(defs.Length, Is.EqualTo(48));
            var maps = defs.SelectMany(d => d.Pages).SelectMany(p => new[] { p.Albedo, p.Normal, p.Light, p.Detail }).Distinct().ToArray();
            Assert.That(maps.Length, Is.EqualTo(192));
            Assert.That(maps.All(t => t != null), Is.True);
            yield return null;
            var rows = new List<MapRow>();
            foreach (var tex in maps)
            {
                string path = AssetDatabase.GetAssetPath(tex);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                int sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Count();
                Assert.That(imp.textureType, Is.EqualTo(TextureImporterType.Default), path);
                Assert.That(imp.sRGBTexture, Is.EqualTo(path.EndsWith("_unlit.png")), path);
                Assert.That(imp.mipmapEnabled, Is.False, path);
                Assert.That(tex.isReadable, Is.False, path);
                Assert.That(tex.filterMode, Is.EqualTo(FilterMode.Point), path);
                Assert.That(sprites, Is.Zero, path);
                rows.Add(new MapRow { path = path, format = tex.format.ToString(), type = imp.textureType.ToString(),
                    width = tex.width, height = tex.height, readable = tex.isReadable, srgb = imp.sRGBTexture,
                    sprites = sprites, bytes = Profiler.GetRuntimeMemorySizeLong(tex) });
            }
            var memory = new MemoryReport { playing = Application.isPlaying, unity = Application.unityVersion,
                device = SystemInfo.graphicsDeviceName, bytes = rows.Sum(r => r.bytes), maps = rows.ToArray() };
            memory.beforeDrawBytes = memory.bytes;
            File.WriteAllText(OutPath + "/runtime-memory.json", JsonUtility.ToJson(memory, true));
            Assert.That(memory.bytes, Is.GreaterThan(0));

            var material = new Material(AssetDatabase.LoadAssetAtPath<Material>(ArtPath + "/Plant5.mat"));
            material.SetFloat("_Sway", 0); material.SetFloat("_FootStrength", 0);
            ShaderUtil.CompilePass(material, 0, true);
            Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
            var profile = AssetDatabase.LoadAssetAtPath<DayNightProfile>("Assets/_Project/Resources/DayNightProfile.asset");
            Assert.That(profile, Is.Not.Null);
            var oldSun = Shader.GetGlobalVector("_SunDir");
            float oldElevation = Shader.GetGlobalFloat("_SunElevation"), oldStrength = Shader.GetGlobalFloat("_ShadowStrength"), oldSnow = Shader.GetGlobalFloat("_FoliageSnow");
            var cells = new List<CellRow>();
            int seed = 0;
            while (PlantFieldScatter.Variant(0, 0, seed) != 0) seed++;
            try
            {
                for (int ti = 0; ti < Hours.Length; ti++)
                {
                    float hour = Hours[ti];
                    var sun = DayNightMath.SunDirection(hour, profile.SunriseHour, profile.SunsetHour, profile.ShadowSouthBias, profile.ShadowNoonLift);
                    Shader.SetGlobalVector("_SunDir", new Vector4(sun.x, sun.y, 0, 0));
                    Shader.SetGlobalFloat("_SunElevation", DayNightMath.SunElevation(hour, profile.SunriseHour, profile.SunsetHour));
                    Shader.SetGlobalFloat("_ShadowStrength", DayNightMath.ShadowStrength(hour, profile.SunriseHour, profile.SunsetHour, 0, profile.OvercastFadesShadow));
                    Shader.SetGlobalFloat("_FoliageSnow", 0);
                    foreach (var def in defs)
                    {
                        int tideIndex = def.TideLadder.Length == 0 ? 0 : ShorePlantTideMath.StateFor(Tides[ti], def.TideLadder);
                        var cell = def.Find(0, 1, 0, tideIndex);
                        Assert.That(cell, Is.Not.Null, def.SpeciesKey);
                        Assert.That(cell.Dormant, Is.False, def.SpeciesKey);
                        var root = new GameObject("PlantPlate");
                        var ground = root.AddComponent<PlantPlateGround>(); ground.Tide = Tides[ti];
                        var recipe = ScriptableObject.CreateInstance<BiomeRecipeDef>();
                        recipe.YoungChance = 0;
                        recipe.Materials = new[] { new PlantMaterialRecipe { Material = 0, Density = 100,
                            Choices = new[] { new PlantChoice { Species = def.Code } } } };
                        var field = root.AddComponent<PlantField>();
                        field.GroundSource = ground; field.Width = field.Height = 1; field.Seed = seed;
                        field.Origin = new Vector2(-.25f - PlantFieldScatter.Offset(0, 0, seed, false, .5f), -.25f - PlantFieldScatter.Offset(0, 0, seed, true, .5f));
                        field.Recipes = new[] { recipe }; field.Species = new[] { def }; field.Material = material;
                        field.Rebuild();
                        Assert.That(field.PlantCount, Is.EqualTo(1));
                        Assert.That(field.ChunkCount, Is.EqualTo(1));
                        foreach (var r in root.GetComponentsInChildren<MeshRenderer>()) r.gameObject.layer = 31;
                        int w = (int)cell.Rect.width, h = (int)cell.Rect.height;
                        var camRoot = new GameObject("PlantPlateCamera");
                        var cam = camRoot.AddComponent<Camera>(); cam.enabled = false;
                        cam.orthographic = true; cam.orthographicSize = h / 64f; cam.aspect = (float)w / h;
                        cam.transform.position = new Vector3((w * .5f - cell.Pivot.x) / 32f, (h * .5f - cell.Pivot.y) / 32f, -10);
                        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.clear;
                        cam.cullingMask = 1 << 31; cam.allowHDR = false; cam.allowMSAA = false;
                        var target = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                        target.Create(); cam.targetTexture = target;
                        var previous = RenderTexture.active;
                        var result = new Texture2D(w, h, TextureFormat.RGBA32, false);
                        try
                        {
                            cam.Render(); cam.Render(); RenderTexture.active = target;
                            result.ReadPixels(new Rect(0, 0, w, h), 0, 0); result.Apply();
                            int opaque = result.GetPixels32().Count(c => c.a > 127);
                            if (def.TideLadder.Length == 0 || Tides[ti] <= 0)
                                Assert.That(opaque, Is.GreaterThan(0), def.SpeciesKey);
                            string file = "unity-" + ti + "-" + def.SpeciesKey + ".png";
                            File.WriteAllBytes(OutPath + "/" + file, result.EncodeToPNG());
                            cells.Add(new CellRow { key = def.SpeciesKey, file = file, timeIndex = ti, tideIndex = tideIndex,
                                hour = hour, tide = Tides[ti], width = w, height = h, opaque = opaque });
                        }
                        finally
                        {
                            RenderTexture.active = previous; cam.targetTexture = null;
                            target.Release(); Object.DestroyImmediate(result); Object.DestroyImmediate(target);
                            Object.DestroyImmediate(camRoot); Object.DestroyImmediate(root); Object.DestroyImmediate(recipe);
                        }
                    }
                }
                File.WriteAllText(OutPath + "/unity-cells.json", JsonUtility.ToJson(new PlateReport { cells = cells.ToArray() }, true));
                Assert.That(cells.Count, Is.EqualTo(240));
                for (int i = 0; i < maps.Length; i++) memory.maps[i].bytes = Profiler.GetRuntimeMemorySizeLong(maps[i]);
                memory.bytes = memory.maps.Sum(r => r.bytes);
                File.WriteAllText(OutPath + "/runtime-memory.json", JsonUtility.ToJson(memory, true));
            }
            finally
            {
                Shader.SetGlobalVector("_SunDir", oldSun); Shader.SetGlobalFloat("_SunElevation", oldElevation);
                Shader.SetGlobalFloat("_ShadowStrength", oldStrength); Shader.SetGlobalFloat("_FoliageSnow", oldSnow);
                Object.DestroyImmediate(material);
            }
        }
    }
}
#endif
