using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace HiddenHarbours.Art.Editor
{
    public static class PlantRig5Baker
    {
        public const string Source = "docs/art/rigs/plants";
        public const string Destination = "Assets/_Project/Art/Foliage/Plants5";
        public const string DefFolder = "Assets/_Project/Data/Plants5";
        // The importer's cap. A larger page would be downscaled, which moves every cell's pixels, so Import refuses it.
        public const int MaxPageSize = 2048;

        [Serializable] public sealed class Catalog { public int ppu; public Species[] species; }
        [Serializable] public sealed class Species
        {
            public string key;
            public Page[] pages;
            public Cell[] cells;
            public Gap[] dormant;
        }
        [Serializable] public sealed class Page { public int w, h; }
        [Serializable] public sealed class Gap { public string stage, season, state; public int variant, tide; }
        [Serializable] public sealed class Cell
        {
            public string stage, season;
            public int variant, tide, page, x, y, w, h;
            public float[] pivot;
            public float heightM;
        }

        [MenuItem("Hidden Harbours/Art/Bake Plant Rig 5")]
        public static void Bake()
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            var start = new ProcessStartInfo("node", "\"" + Path.Combine(project, Source, "bake.cjs") + "\" \"" + Path.Combine(project, Destination) + "\"")
            { WorkingDirectory = project, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using (var process = Process.Start(start))
            {
                var output = process.StandardOutput.ReadToEndAsync(); var error = process.StandardError.ReadToEndAsync();
                process.WaitForExit();
                if (process.ExitCode != 0) throw new InvalidOperationException(error.Result);
                Debug.Log(output.Result);
            }
            Import();
        }

        public static void Import()
        {
            var cat = JsonUtility.FromJson<Catalog>(File.ReadAllText(Source + "/catalog.json"));
            if (cat.ppu != 32 || cat.species.Length != 50) throw new InvalidOperationException("Plant catalog changed");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            int gaps = 0;
            foreach (var sp in cat.species)
            {
                var def = AssetDatabase.LoadAssetAtPath<PlantSpeciesDef>(DefFolder + "/Plant_" + sp.key + ".asset");
                if (def == null || def.SpeciesKey != sp.key) throw new InvalidOperationException("Missing plant Def " + sp.key);
                def.Pages = new PlantSheetPage[sp.pages.Length];
                for (int p = 0; p < sp.pages.Length; p++)
                {
                    if (sp.pages[p].w > MaxPageSize || sp.pages[p].h > MaxPageSize) throw new InvalidOperationException("Plant atlas too large");
                    def.Pages[p] = new PlantSheetPage { Albedo = Load(sp.key, p, "unlit", sp.pages[p]),
                        Normal = Load(sp.key, p, "normal", sp.pages[p]), Light = Load(sp.key, p, "light", sp.pages[p]),
                        Detail = Load(sp.key, p, "detail", sp.pages[p]) };
                }
                def.Cells = new PlantCell[sp.cells.Length + sp.dormant.Length];
                for (int i = 0; i < sp.cells.Length; i++)
                {
                    var c = sp.cells[i];
                    int stage = Array.IndexOf(new[] { "mature", "young", "seedling" }, c.stage);
                    int season = Array.IndexOf(new[] { "spring", "summer", "autumn", "winter" }, c.season);
                    if (stage < 0 || season < 0) throw new InvalidOperationException("Unknown plant state");
                    def.Cells[i] = new PlantCell { Stage = stage, Season = season, Variant = c.variant, Tide = c.tide, Page = c.page,
                        Rect = new Rect(c.x, sp.pages[c.page].h - c.y - c.h, c.w, c.h),
                        Pivot = new Vector2(c.pivot[0], c.h - c.pivot[1]), HeightM = c.heightM };
                }
                for (int i = 0; i < sp.dormant.Length; i++)
                {
                    var d = sp.dormant[i];
                    if (d.state != "DORMANT") throw new InvalidOperationException("Unknown dormant state");
                    def.Cells[sp.cells.Length + i] = new PlantCell { Dormant = true,
                        Stage = Array.IndexOf(new[] { "mature", "young", "seedling" }, d.stage),
                        Season = Array.IndexOf(new[] { "spring", "summer", "autumn", "winter" }, d.season),
                        Variant = d.variant, Tide = d.tide };
                }
                gaps += sp.dormant.Length;
                EditorUtility.SetDirty(def);
            }
            var shader = Shader.Find("HiddenHarbours/Plant");
            if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Plant shader did not compile");
            string matPath = Destination + "/Plant5.mat";
            if (AssetDatabase.LoadAssetAtPath<Material>(matPath) == null)
                AssetDatabase.CreateAsset(new Material(shader), matPath);
            AssetDatabase.SaveAssets();
            Debug.Log("Plant rig 5: imported 50 species. Dormant cells: " + gaps + ".");
        }
        static Texture2D Load(string key, int page, string channel, Page spec)
        {
            string path = Destination + "/" + key + "_" + page + "_" + channel + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing plant map " + path);
            importer.textureType = TextureImporterType.Default;
            importer.spriteImportMode = SpriteImportMode.None;
            // Clear the old Sprite table even though this map now imports as a texture.
#pragma warning disable 0618
            importer.spritesheet = Array.Empty<SpriteMetaData>();
#pragma warning restore 0618
            importer.sRGBTexture = channel == "unlit"; importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point; importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None; importer.maxTextureSize = MaxPageSize;
            importer.alphaIsTransparency = false; importer.isReadable = false;
            // The detail map's part moves from b to r on every platform, so the shader reads .r whatever the format.
            importer.swizzleR = channel == "detail" ? TextureImporterSwizzle.B : TextureImporterSwizzle.R;
            importer.swizzleG = TextureImporterSwizzle.G; importer.swizzleB = TextureImporterSwizzle.B; importer.swizzleA = TextureImporterSwizzle.A;
            // DefaultTexturePlatform stays Uncompressed (RGBA32, the exact pixels) for every platform without an
            // override. Standalone names each kind's format instead of trusting Automatic, as ADR 0046 does for R16.
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings { name = "Standalone", overridden = true,
                maxTextureSize = MaxPageSize, textureCompression = TextureImporterCompression.Uncompressed, format = StandaloneFormat(channel) });
            importer.SaveAndReimport();
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null || tex.width != spec.w || tex.height != spec.h) throw new InvalidOperationException("Plant map size changed " + path);
            return tex;
        }
        // The narrowest uncompressed format that keeps every channel HiddenHarboursPlant.shader reads, byte for byte
        // (art bible §9.2: Compression None). It reads all of unlit (rgba) and normal (rgb); r (sky) and g (through)
        // of light, never b (height); and only the part of detail, which the swizzle above has moved to r.
        // Block formats need whole 4x4 blocks and change pixels: BC7 moved a quarter of the drawn albedo texels.
        static TextureImporterFormat StandaloneFormat(string channel) =>
            channel == "light" ? TextureImporterFormat.RG16 : channel == "detail" ? TextureImporterFormat.R8 : TextureImporterFormat.RGBA32;
    }
}
