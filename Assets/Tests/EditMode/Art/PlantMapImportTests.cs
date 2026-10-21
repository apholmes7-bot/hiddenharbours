using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.Art.EditMode
{
    public class PlantMapImportTests
    {
        const string MapFolder = "Assets/_Project/Art/Foliage/Plants5";
        const string ShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursPlant.shader";

        // Each kind of plant map's import, stated here rather than read from PlantRig5Baker, so a change to the baker's
        // rule has to change this table too. Only the albedo (unlit) is colour. Standalone keeps, uncompressed and byte
        // for byte, every channel HiddenHarboursPlant.shader reads: all of unlit and normal, r and g of light, and the
        // detail map's part, which the swizzle moves from b to r. Every other platform imports the default, RGBA32.
        static readonly (string kind, bool srgb, TextureImporterFormat standalone, TextureFormat loaded, TextureImporterSwizzle r)[] Kinds =
        {
            ("unlit", true, TextureImporterFormat.RGBA32, TextureFormat.RGBA32, TextureImporterSwizzle.R),
            ("normal", false, TextureImporterFormat.RGBA32, TextureFormat.RGBA32, TextureImporterSwizzle.R),
            ("light", false, TextureImporterFormat.RG16, TextureFormat.RG16, TextureImporterSwizzle.R),
            ("detail", false, TextureImporterFormat.R8, TextureFormat.R8, TextureImporterSwizzle.B),
        };

        [Test] public void EveryPlantMapIsDefaultWithNoSpriteModeOrSliceRows()
        {
            var maps = Directory.GetFiles("Assets/_Project/Art/Foliage/Plants5", "*.png");
            Assert.That(maps.Length, Is.GreaterThan(0));
            var faults = new List<string>();
            foreach (string path in maps)
            {
                var importer = AssetImporter.GetAtPath(path.Replace('\\', '/')) as TextureImporter;
                if (importer == null) { faults.Add(path + ": missing importer"); continue; }
                if (importer.textureType != TextureImporterType.Default) faults.Add(path + ": type is not Default");
                if (importer.spriteImportMode != SpriteImportMode.None) faults.Add(path + ": sprite mode is not None");
#pragma warning disable 0618
                if (importer.spritesheet.Length != 0) faults.Add(path + ": slice rows remain");
#pragma warning restore 0618
            }
            Assert.That(faults, Is.Empty, string.Join("\n", faults));
        }

        [Test] public void EveryPlantMapNamesItsKindsStandaloneFormatAndKeepsExactPixelSettings()
        {
            var faults = new List<string>();
            var kindsByPage = new Dictionary<string, List<string>>();
            foreach (string path in Maps())
            {
                var kind = KindOf(path);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (kind.kind == null || importer == null) { faults.Add(path + ": not one of the four plant map kinds"); continue; }
                string page = path.Substring(0, path.Length - (kind.kind + ".png").Length);
                if (!kindsByPage.TryGetValue(page, out var kinds)) kindsByPage[page] = kinds = new List<string>();
                kinds.Add(kind.kind);
                var standalone = importer.GetPlatformTextureSettings("Standalone");
                var fallback = importer.GetDefaultPlatformTextureSettings();
                var size = PngSize(path);
                void Check(bool ok, string fault) { if (!ok) faults.Add(path + ": " + fault); }
                Check(importer.sRGBTexture == kind.srgb, "sRGB is " + importer.sRGBTexture);
                Check(importer.filterMode == FilterMode.Point, "filter is " + importer.filterMode);
                Check(!importer.mipmapEnabled, "has mips");
                Check(!importer.isReadable, "keeps a CPU copy in the player");
                Check(importer.wrapModeU == TextureWrapMode.Clamp && importer.wrapModeV == TextureWrapMode.Clamp, "does not clamp");
                Check(importer.npotScale == TextureImporterNPOTScale.None, "NPOT scale is " + importer.npotScale);
                Check(!importer.alphaIsTransparency, "alpha is transparency, which recolours the clear texels");
                Check(fallback.format == TextureImporterFormat.Automatic && fallback.textureCompression == TextureImporterCompression.Uncompressed,
                    "the default platform is " + fallback.format + "/" + fallback.textureCompression + ", not RGBA32 through Uncompressed");
                Check(importer.swizzleR == kind.r && importer.swizzleG == TextureImporterSwizzle.G && importer.swizzleB == TextureImporterSwizzle.B
                    && importer.swizzleA == TextureImporterSwizzle.A, "swizzle is " + importer.swizzleR + importer.swizzleG + importer.swizzleB + importer.swizzleA);
                Check(standalone.overridden && standalone.format == kind.standalone && standalone.textureCompression == TextureImporterCompression.Uncompressed,
                    "Standalone is " + (standalone.overridden ? standalone.format + "/" + standalone.textureCompression : "not overridden") + ", not " + kind.standalone);
                var others = OverriddenPlatforms(path).Where(p => p != "Standalone").ToArray();
                Check(others.Length == 0, "overrides " + string.Join(", ", others) + ", which would leave the default's exact pixels");
                Check(Math.Max(size.x, size.y) <= Math.Min(importer.maxTextureSize, standalone.maxTextureSize),
                    "a size cap would downscale its " + size.x + "x" + size.y + " pixels");
            }
            foreach (var page in kindsByPage)
                if (page.Value.Count != Kinds.Length) faults.Add(page.Key + "*.png: has " + string.Join(", ", page.Value) + " of the four maps");
            Assert.That(kindsByPage, Is.Not.Empty);
            Assert.That(faults, Is.Empty, string.Join("\n", faults));
        }

        [Test] public void EveryPlantMapImportsToItsKindsFormatAtItsOwnSize()
        {
            // The editor imports for its active build target, and CI runs it on a Standalone one, which is how
            // SeabedHeightImportTests pins R16 too. An editor on any other target imports RGBA32 and fails here, as it does there.
            var faults = new List<string>();
            foreach (string path in Maps())
            {
                var kind = KindOf(path);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (kind.kind == null || tex == null) { faults.Add(path + ": not a plant map texture"); continue; }
                var size = PngSize(path);
                if (tex.format != kind.loaded) faults.Add(path + ": loads as " + tex.format + ", not its Standalone " + kind.loaded);
                if (tex.width != size.x || tex.height != size.y) faults.Add(path + ": loads at " + tex.width + "x" + tex.height + ", not its PNG's " + size.x + "x" + size.y);
                if (tex.mipmapCount != 1) faults.Add(path + ": loads with " + tex.mipmapCount + " mips");
            }
            Assert.That(faults, Is.Empty, string.Join("\n", faults));
        }

        [Test] public void ThePlantShaderReadsPartFromRAndNeverReadsLightB()
        {
            // Comments dropped, so a note about a channel can neither pass nor fail the read.
            string source = Regex.Replace(File.ReadAllText(ShaderPath), "//[^\n]*", "");
            var detail = Regex.Matches(source, @"SAMPLE_TEXTURE2D\(\s*_DetailTex\s*,[^)]*\)(\s*\.\s*\w+)?").Cast<Match>()
                .Select(m => Regex.Replace(m.Groups[1].Value, @"\s", "")).ToArray();
            Assert.That(detail, Is.Not.Empty, "the shader no longer samples _DetailTex");
            Assert.That(detail, Is.All.EqualTo(".r"), "the detail map imports as R8 on Standalone, so part must be read from r");
            Assert.That(Regex.Matches(source, @"SAMPLE_TEXTURE2D\(\s*_LightTex\b").Count, Is.EqualTo(1), "the light map is sampled once");
            Assert.That(Regex.IsMatch(source, @"\blight\s*=\s*SAMPLE_TEXTURE2D\(\s*_LightTex\b"), Is.True, "the light sample is no longer stored in light");
            // Every use of the sample after it is stored: RG16 keeps r (sky) and g (through) and reads b (height) as 0.
            var uses = Regex.Matches(source, @"\blight\b(?!\s*=[^=])(\s*\.\s*\w+)?").Cast<Match>()
                .Select(m => Regex.Replace(m.Groups[1].Value, @"\s", "")).ToArray();
            Assert.That(uses, Is.Not.Empty, "the shader no longer reads the light sample");
            Assert.That(uses, Is.All.Matches<string>(u => u == ".r" || u == ".g"), "the light map imports as RG16 on Standalone: " + string.Join(" ", uses));
        }

        static string[] Maps()
        {
            var maps = Directory.GetFiles(MapFolder, "*.png").Select(p => p.Replace('\\', '/')).OrderBy(p => p, StringComparer.Ordinal).ToArray();
            Assert.That(maps.Length, Is.GreaterThan(0));
            return maps;
        }

        static (string kind, bool srgb, TextureImporterFormat standalone, TextureFormat loaded, TextureImporterSwizzle r) KindOf(string path) =>
            Kinds.FirstOrDefault(k => path.EndsWith("_" + k.kind + ".png", StringComparison.Ordinal));

        // A PNG's own size, from its IHDR: width and height are big-endian at bytes 16 and 20.
        static Vector2Int PngSize(string path)
        {
            var header = new byte[24];
            using (var file = File.OpenRead(path)) Assert.That(file.Read(header, 0, header.Length), Is.EqualTo(header.Length), path);
            Assert.That(header.Take(8), Is.EqualTo(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }), path + " is not a PNG");
            return new Vector2Int(header[16] << 24 | header[17] << 16 | header[18] << 8 | header[19],
                header[20] << 24 | header[21] << 16 | header[22] << 8 | header[23]);
        }

        // The platforms a map's .meta overrides, read from its platformSettings rows.
        static List<string> OverriddenPlatforms(string path)
        {
            var overridden = new List<string>(); string target = null;
            foreach (string line in File.ReadAllLines(path + ".meta"))
            {
                string text = line.Trim();
                if (text.StartsWith("buildTarget: ", StringComparison.Ordinal)) target = text.Substring("buildTarget: ".Length);
                else if (text == "overridden: 1" && target != null) overridden.Add(target);
            }
            return overridden;
        }
    }
}
