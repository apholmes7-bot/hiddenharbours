#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
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
        // The page plates: noon, bare, then half and full snow cover (the shader's up step at 0.545 and at 0.07).
        const float PlateHour = 12f;
        static readonly float[] SnowCover = { 0f, .5f, 1f };
        const float Ppu = 32f;

        [Serializable] public sealed class MapRow
        {
            public string path, format, type;
            public int width, height, sprites;
            public bool readable, srgb;
            public long bytes, formatBytes;
        }
        [Serializable] public sealed class MemoryReport
        {
            public bool playing;
            public string unity, device;
            // bytes: the editor's Profiler count, which includes the CPU copy the editor keeps of every texture.
            // formatBytes: each map's w x h x bytes per texel of its GPU format, which is what a player holds.
            public long bytes, beforeDrawBytes, formatBytes;
            public MapRow[] maps;
        }
        [Serializable] public sealed class ReadbackRow
        {
            public string path, format, swizzle;
            public int texels, channels;
            public long differing;
        }
        [Serializable] public sealed class ReadbackReport
        {
            public string unity, device;
            public long differing;
            public ReadbackRow[] maps;
        }
        [Serializable] public sealed class PageRow
        {
            public string key, file;
            public int page, snowIndex, width, height, drawn, opaque, kept, snowed;
            public float snow;
            public long differing;
        }
        [Serializable] public sealed class PageReport
        {
            public string unity, device;
            public float hour;
            public long differing;
            public PageRow[] pages;
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
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("SKIPPED, NOT VERIFIED — no graphics device. These plates and the texture-memory read need the local GPU.");
            Assert.That(Application.isPlaying, Is.True);
            Directory.CreateDirectory(OutPath);
            var defs = AssetDatabase.FindAssets("t:PlantSpeciesDef", new[] { DefPath })
                .Select(g => AssetDatabase.LoadAssetAtPath<PlantSpeciesDef>(AssetDatabase.GUIDToAssetPath(g)))
                .OrderBy(d => d.Code).ToArray();
            Assert.That(defs.Length, Is.EqualTo(50));
            var maps = defs.SelectMany(d => d.Pages).SelectMany(p => new[] { p.Albedo, p.Normal, p.Light, p.Detail }).Distinct().ToArray();
            Assert.That(maps.Length, Is.EqualTo(200));
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
                    sprites = sprites, bytes = Profiler.GetRuntimeMemorySizeLong(tex), formatBytes = FormatBytes(tex) });
            }
            var memory = new MemoryReport { playing = Application.isPlaying, unity = Application.unityVersion,
                device = SystemInfo.graphicsDeviceName, bytes = rows.Sum(r => r.bytes), formatBytes = rows.Sum(r => r.formatBytes), maps = rows.ToArray() };
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
                Assert.That(cells.Count, Is.EqualTo(250));
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

        // The decoded-pixel proof: every map's bytes as the GPU holds them, against the source PNG read from disk.
        // Each GPU channel must be the PNG channel the importer's swizzle names for it, in every texel.
        [UnityTest]
        public IEnumerator EveryPlantMapReadsBackItsSourcePngBytes()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("SKIPPED, NOT VERIFIED — no graphics device. The map readback needs the local GPU.");
            Assert.That(SystemInfo.supportsAsyncGPUReadback, Is.True);
            Directory.CreateDirectory(OutPath);
            var maps = Defs().SelectMany(d => d.Pages).SelectMany(p => new[] { p.Albedo, p.Normal, p.Light, p.Detail }).Distinct().ToArray();
            Assert.That(maps.Length, Is.EqualTo(200));
            yield return null;
            var rows = new List<ReadbackRow>();
            foreach (var tex in maps)
            {
                string path = AssetDatabase.GetAssetPath(tex);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                var swizzle = new[] { imp.swizzleR, imp.swizzleG, imp.swizzleB, imp.swizzleA };
                Assert.That(tex.format == TextureFormat.RGBA32 || tex.format == TextureFormat.RG16 || tex.format == TextureFormat.R8, Is.True, path + " is " + tex.format);
                var src = PngPixels(path, out int width, out int height);
                Assert.That(new Vector2Int(tex.width, tex.height), Is.EqualTo(new Vector2Int(width, height)), path);
                var request = AsyncGPUReadback.Request(tex, 0);
                request.WaitForCompletion();
                Assert.That(request.hasError, Is.False, path);
                byte[] gpu = request.GetData<byte>().ToArray();
                int channels = gpu.Length / src.Length;
                Assert.That(channels * src.Length, Is.EqualTo(gpu.Length), path);
                long differing = 0;
                for (int i = 0; i < src.Length; i++)
                    for (int c = 0; c < channels; c++)
                        if (gpu[i * channels + c] != Channel(src[i], swizzle[c])) differing++;
                rows.Add(new ReadbackRow { path = path, format = tex.format.ToString(), swizzle = string.Join(",", swizzle),
                    texels = src.Length, channels = channels, differing = differing });
            }
            var report = new ReadbackReport { unity = Application.unityVersion, device = SystemInfo.graphicsDeviceName,
                differing = rows.Sum(r => r.differing), maps = rows.ToArray() };
            File.WriteAllText(OutPath + "/map-readback.json", JsonUtility.ToJson(report, true));
            Assert.That(report.differing, Is.Zero, "texel channels that differ from the source PNG");
        }

        // Every page drawn whole through the plant shader, one texel per pixel, at noon, bare and under snow: once from its
        // imported maps and once from its source PNGs at RGBA32. They must match pixel for pixel. Snow is where the detail
        // map's part and the light map's r step, so a channel the import moved or dropped wrongly shows there.
        [UnityTest]
        public IEnumerator EveryPageDrawsTheSameFromItsImportedMapsAsFromItsSourcePngs()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("SKIPPED, NOT VERIFIED — no graphics device. These page plates need the local GPU.");
            Assert.That(Application.isPlaying, Is.True);
            Directory.CreateDirectory(OutPath);
            var defs = Defs();
            var material = new Material(AssetDatabase.LoadAssetAtPath<Material>(ArtPath + "/Plant5.mat"));
            material.SetFloat("_Sway", 0); material.SetFloat("_FootStrength", 0);
            ShaderUtil.CompilePass(material, 0, true);
            Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
            var profile = AssetDatabase.LoadAssetAtPath<DayNightProfile>("Assets/_Project/Resources/DayNightProfile.asset");
            Assert.That(profile, Is.Not.Null);
            var oldSun = Shader.GetGlobalVector("_SunDir");
            float oldElevation = Shader.GetGlobalFloat("_SunElevation"), oldStrength = Shader.GetGlobalFloat("_ShadowStrength"), oldSnow = Shader.GetGlobalFloat("_FoliageSnow");
            var rows = new List<PageRow>();
            try
            {
                var sun = DayNightMath.SunDirection(PlateHour, profile.SunriseHour, profile.SunsetHour, profile.ShadowSouthBias, profile.ShadowNoonLift);
                Shader.SetGlobalVector("_SunDir", new Vector4(sun.x, sun.y, 0, 0));
                Shader.SetGlobalFloat("_SunElevation", DayNightMath.SunElevation(PlateHour, profile.SunriseHour, profile.SunsetHour));
                Shader.SetGlobalFloat("_ShadowStrength", DayNightMath.ShadowStrength(PlateHour, profile.SunriseHour, profile.SunsetHour, 0, profile.OvercastFadesShadow));
                foreach (var def in defs)
                {
                    for (int p = 0; p < def.Pages.Length; p++)
                    {
                        var page = def.Pages[p];
                        var albedo = PngPixels(AssetDatabase.GetAssetPath(page.Albedo), out _, out _);
                        int opaque = albedo.Count(c => c.a == byte.MaxValue), kept = albedo.Count(c => c.a >= ClipKeepsAlpha);
                        var source = new[] { SourceMap(page.Albedo, false, false), SourceMap(page.Normal, true, false),
                            SourceMap(page.Light, true, false), SourceMap(page.Detail, true, true) };
                        Color32[] bare = null;
                        try
                        {
                            for (int si = 0; si < SnowCover.Length; si++)
                            {
                                Shader.SetGlobalFloat("_FoliageSnow", SnowCover[si]);
                                var fromImport = DrawPage(material, page.Albedo, page.Normal, page.Light, page.Detail);
                                var fromSource = DrawPage(material, source[0], source[1], source[2], source[3]);
                                try
                                {
                                    var drawnImport = fromImport.GetPixels32(); var drawnSource = fromSource.GetPixels32();
                                    if (si == 0) bare = drawnImport;
                                    long differing = 0; int drawn = 0, snowed = 0;
                                    for (int i = 0; i < drawnImport.Length; i++)
                                    {
                                        if (!Same(drawnImport[i], drawnSource[i])) differing++;
                                        if (drawnImport[i].a > 0) drawn++;
                                        if (!Same(drawnImport[i], bare[i])) snowed++;
                                    }
                                    string file = "page-" + si + "-" + def.SpeciesKey + "_" + p + ".png";
                                    File.WriteAllBytes(OutPath + "/" + file, fromImport.EncodeToPNG());
                                    rows.Add(new PageRow { key = def.SpeciesKey, file = file, page = p, snowIndex = si, snow = SnowCover[si],
                                        width = fromImport.width, height = fromImport.height, drawn = drawn, opaque = opaque, kept = kept,
                                        snowed = snowed, differing = differing });
                                }
                                finally { Object.DestroyImmediate(fromImport); Object.DestroyImmediate(fromSource); }
                            }
                        }
                        finally { foreach (var t in source) Object.DestroyImmediate(t); }
                        yield return null;
                    }
                }
                var report = new PageReport { unity = Application.unityVersion, device = SystemInfo.graphicsDeviceName, hour = PlateHour,
                    differing = rows.Sum(r => r.differing), pages = rows.ToArray() };
                File.WriteAllText(OutPath + "/unity-pages.json", JsonUtility.ToJson(report, true));
                Assert.That(rows.Count, Is.EqualTo(defs.Sum(d => d.Pages.Length) * SnowCover.Length));
                // A blank draw, or both draws falling back to a default map, would match pixel for pixel, so each page must
                // cover at least its opaque texels and no more than the texels the clip keeps.
                Assert.That(rows.Where(r => r.drawn == 0 || r.drawn < r.opaque || r.drawn > r.kept).Select(r => r.file), Is.Empty,
                    "pages whose draw does not cover their own albedo");
                Assert.That(rows.Where(r => r.snowIndex == SnowCover.Length - 1).Sum(r => (long)r.snowed), Is.GreaterThan(0), "full cover drew no snow");
                Assert.That(report.differing, Is.Zero, "pixels that differ between the imported maps and the source PNGs");
            }
            finally
            {
                Shader.SetGlobalVector("_SunDir", oldSun); Shader.SetGlobalFloat("_SunElevation", oldElevation);
                Shader.SetGlobalFloat("_ShadowStrength", oldStrength); Shader.SetGlobalFloat("_FoliageSnow", oldSnow);
                Object.DestroyImmediate(material);
            }
        }

        // clip(albedo.a - 0.01) in HiddenHarboursPlant.shader keeps every texel whose alpha is 3/255 or more. The pass
        // blends SrcAlpha OneMinusSrcAlpha onto a clear target, so a kept texel writes alpha a*a/255, which rounds to 0
        // below about 12/255: a page's drawn pixels lie between its opaque texels and its kept ones.
        const byte ClipKeepsAlpha = 3;

        static PlantSpeciesDef[] Defs()
        {
            var defs = AssetDatabase.FindAssets("t:PlantSpeciesDef", new[] { DefPath })
                .Select(g => AssetDatabase.LoadAssetAtPath<PlantSpeciesDef>(AssetDatabase.GUIDToAssetPath(g)))
                .OrderBy(d => d.Code).ToArray();
            Assert.That(defs.Length, Is.EqualTo(50));
            return defs;
        }

        // A map's GPU format weighed by hand: whole blocks of the format's block size (a block is one texel for R8, RG16
        // and RGBA32, 4x4 for a BC format). No mips: the import turns them off.
        static long FormatBytes(Texture tex)
        {
            var format = tex.graphicsFormat;
            long blockW = GraphicsFormatUtility.GetBlockWidth(format), blockH = GraphicsFormatUtility.GetBlockHeight(format);
            return (tex.width + blockW - 1) / blockW * ((tex.height + blockH - 1) / blockH) * GraphicsFormatUtility.GetBlockSize(format);
        }

        // A map exactly as its PNG on disk holds it, decoded here rather than by Unity so the import is checked against
        // an independent read. The plant rig writes 8-bit RGBA PNGs without interlace, which is all this reads. Rows come
        // out bottom up, as the imported texture holds them.
        static Color32[] PngPixels(string path, out int width, out int height)
        {
            byte[] png = File.ReadAllBytes(path);
            if (png.Length < 8 || png[0] != 137 || png[1] != 'P' || png[2] != 'N' || png[3] != 'G') throw new InvalidDataException(path + " is not a PNG");
            var data = new MemoryStream(); width = height = 0;
            for (int at = 8; at + 8 <= png.Length;)
            {
                int length = BigEndian(png, at); string type = Encoding.ASCII.GetString(png, at + 4, 4);
                if (type == "IHDR")
                {
                    width = BigEndian(png, at + 8); height = BigEndian(png, at + 12);
                    // Bit depth 8, colour type 6 (RGBA), interlace 0.
                    if (png[at + 16] != 8 || png[at + 17] != 6 || png[at + 20] != 0) throw new InvalidDataException(path + " is not 8-bit RGBA without interlace");
                }
                else if (type == "IDAT") data.Write(png, at + 8, length);
                at += 12 + length;
            }
            int stride = width * 4;
            var raw = new byte[(stride + 1) * height];
            // The zlib stream's two header bytes come before its deflate data.
            using (var inflate = new DeflateStream(new MemoryStream(data.GetBuffer(), 2, (int)data.Length - 2), CompressionMode.Decompress))
                for (int read = 0, n; read < raw.Length; read += n)
                    if ((n = inflate.Read(raw, read, raw.Length - read)) == 0) throw new InvalidDataException(path + " ends early");
            var pixels = new Color32[width * height];
            byte[] row = new byte[stride], above = new byte[stride];
            for (int y = 0; y < height; y++)
            {
                int filter = raw[y * (stride + 1)], at = y * (stride + 1) + 1;
                if (filter > 4) throw new InvalidDataException(path + " has row filter " + filter);
                for (int x = 0; x < stride; x++)
                {
                    // PNG's five row filters: none, sub, up, average, Paeth.
                    int left = x >= 4 ? row[x - 4] : 0, up = above[x], corner = x >= 4 ? above[x - 4] : 0, guess = left + up - corner;
                    int toLeft = Math.Abs(guess - left), toUp = Math.Abs(guess - up), toCorner = Math.Abs(guess - corner);
                    int predict = filter == 1 ? left : filter == 2 ? up : filter == 3 ? (left + up) / 2
                        : filter == 4 ? (toLeft <= toUp && toLeft <= toCorner ? left : toUp <= toCorner ? up : corner) : 0;
                    row[x] = (byte)(raw[at + x] + predict);
                }
                for (int x = 0; x < width; x++) pixels[(height - 1 - y) * width + x] = new Color32(row[4 * x], row[4 * x + 1], row[4 * x + 2], row[4 * x + 3]);
                var done = above; above = row; row = done;
            }
            return pixels;
        }

        static int BigEndian(byte[] bytes, int at) => bytes[at] << 24 | bytes[at + 1] << 16 | bytes[at + 2] << 8 | bytes[at + 3];

        // A map as its source PNG holds it, at RGBA32 in the import's colour space, Point and Clamp, no mips. Part is the
        // detail PNG's b and the shader reads it from r, so the source copies b into r (main's shader read the same byte as .b).
        static Texture2D SourceMap(Texture2D imported, bool linear, bool partToR)
        {
            var pixels = PngPixels(AssetDatabase.GetAssetPath(imported), out int width, out int height);
            if (partToR) for (int i = 0; i < pixels.Length; i++) pixels[i].r = pixels[i].b;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false, linear) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(pixels); tex.Apply(false, true);
            return tex;
        }

        // One page through the plant shader at one texel per pixel: a quad over the whole page with its roots at height 0,
        // so nothing bends, seen square-on by an orthographic camera the page's size.
        static Texture2D DrawPage(Material material, Texture albedo, Texture normal, Texture light, Texture detail)
        {
            int w = albedo.width, h = albedo.height; float mw = w / Ppu, mh = h / Ppu;
            var mesh = new Mesh { name = "PlantPagePlate", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(new List<Vector3> { new Vector3(0, 0, 0), new Vector3(mw, 0, 0), new Vector3(0, mh, 0), new Vector3(mw, mh, 0) });
            mesh.SetUVs(0, new List<Vector2> { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) });
            var rootAtGround = new Vector4(0, 0, 0, 1);
            mesh.SetUVs(1, new List<Vector4> { rootAtGround, rootAtGround, rootAtGround, rootAtGround });
            mesh.SetTriangles(new[] { 0, 2, 1, 1, 2, 3 }, 0);
            var root = new GameObject("PlantPagePlate") { layer = 31 };
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = root.AddComponent<MeshRenderer>(); mr.sharedMaterial = material;
            var props = new MaterialPropertyBlock(); props.SetTexture("_MainTex", albedo); props.SetTexture("_NormalTex", normal);
            props.SetTexture("_LightTex", light); props.SetTexture("_DetailTex", detail); mr.SetPropertyBlock(props);
            var camRoot = new GameObject("PlantPageCamera");
            var cam = camRoot.AddComponent<Camera>(); cam.enabled = false;
            cam.orthographic = true; cam.orthographicSize = mh / 2; cam.aspect = (float)w / h;
            cam.transform.position = new Vector3(mw / 2, mh / 2, -10);
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
                return result;
            }
            catch { Object.DestroyImmediate(result); throw; }
            finally
            {
                RenderTexture.active = previous; cam.targetTexture = null; target.Release();
                Object.DestroyImmediate(target); Object.DestroyImmediate(camRoot); Object.DestroyImmediate(root); Object.DestroyImmediate(mesh);
            }
        }

        static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;

        static byte Channel(Color32 c, TextureImporterSwizzle s)
        {
            switch (s)
            {
                case TextureImporterSwizzle.R: return c.r;
                case TextureImporterSwizzle.G: return c.g;
                case TextureImporterSwizzle.B: return c.b;
                case TextureImporterSwizzle.A: return c.a;
                default: throw new InvalidOperationException("Unexpected swizzle " + s);
            }
        }
    }
}
#endif
