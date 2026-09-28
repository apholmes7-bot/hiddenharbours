using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Art.Editor;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>CPU-only import lifecycle and byte/identity guards for ADR 0047. No missing-kit skips.</summary>
    [NonParallelizable]
    public class TerrainArrayImporterTests
    {
        const ImportAssetOptions Sync = ImportAssetOptions.ForceSynchronousImport;
        const string BaselinePath = "Assets/Tests/EditMode/Fixtures/TerrainArrayByteBaseline.json";

        [Test]
        public void ReimportTwice_PreservesEveryByteAndObjectId()
        {
            ReimportProduction();
            string[] first = Snapshot(TerrainArrayAssets.DetailRecipePath, TerrainArrayAssets.RelightRecipePath, true);
            ReimportProduction();
            CollectionAssert.AreEqual(first,
                Snapshot(TerrainArrayAssets.DetailRecipePath, TerrainArrayAssets.RelightRecipePath, true));
        }

        [Test]
        public void ImportedObjects_KeepTextureContractAndFullDepth()
        {
            // Required loads check dimensions, format, colour space, sampling, mips and readability.
            var detail = TerrainArrayAssets.LoadDetailRequired();
            var relight = TerrainArrayAssets.LoadRelightRequired();
            Assert.AreEqual(63, detail.depth, "The shipped slice ABI must be deliberately amended when the kit grows.");
            CollectionAssert.AreEquivalent(new[] { TerrainArrayAssets.DetailName },
                Textures(TerrainArrayAssets.DetailRecipePath).Select(t => t.name).ToArray());
            CollectionAssert.AreEquivalent(new[] { "TerrainRelightNormal", "TerrainRelightLight", "TerrainRelightDetail",
                    "TerrainRelightRamp" }, Textures(TerrainArrayAssets.RelightRecipePath).Select(t => t.name).ToArray());
            Assert.AreEqual(81, relight.Ramp.width);
            Assert.AreEqual(detail.depth, relight.Ramp.height);
            Assert.AreEqual(0, relight.Ramp.anisoLevel);
        }

        [Test]
        public void PackedBytes_MatchThePreMigrationBaseline()
        {
            var baseline = JsonUtility.FromJson<Baseline>(File.ReadAllText(BaselinePath));
            var detail = TerrainArrayAssets.LoadDetailRequired();
            var relight = TerrainArrayAssets.LoadRelightRequired();
            CollectionAssert.AreEqual(baseline.detailMipSha256, ArrayHashes(detail), "Detail mip bytes changed.");
            CollectionAssert.AreEqual(baseline.relightSha256,
                new[] { ArrayHashes(relight.Normal)[0], ArrayHashes(relight.Light)[0], ArrayHashes(relight.Detail)[0] },
                "Relight map bytes changed.");
            Assert.AreEqual(baseline.rampSha256, RampHash(relight.Ramp), "Ramp float bits changed.");
        }

        [Test]
        public void SourcePngChange_ReimportsWithoutRecipeReimport()
        {
            using var fixture = new Fixture();
            string path = fixture.Root + "/Path_Hi.png";
            ChangePixel(path, 0);
            ImportSource(path);
            AssertSourcePixel(fixture.DetailPath, path, 62, null, 0);
            for (int a = 0; a < TerrainTexArrayBuilder.RelightMapSuffixes.Length; a++)
            {
                path = fixture.Root + "/Path_Hi" + TerrainTexArrayBuilder.RelightMapSuffixes[a] + ".png";
                ChangePixel(path, a + 1);
                ImportSource(path);
                AssertSourcePixel(fixture.RelightPath, path, 62, a, a + 1);
            }

            path = fixture.Root + "/Path_Hi.png";
            byte[] saved = File.ReadAllBytes(path);
            File.Delete(path);
            ExpectError(path);
            AssetDatabase.Refresh(Sync);
            StringAssert.Contains(path, Assert.Throws<InvalidOperationException>(
                () => TerrainArrayAssets.LoadDetailRequired(fixture.DetailPath)).Message);
            File.WriteAllBytes(path, saved);
            ImportSource(path);
            AssertSourcePixel(fixture.DetailPath, path, 62, null, 0);
        }

        [Test]
        public void ManifestChange_ReimportsWithoutRecipeReimport()
        {
            using var fixture = new Fixture();
            var before = TerrainArrayAssets.LoadRelightRequired(fixture.RelightPath);
            string[] maps = { ArrayHashes(before.Normal)[0], ArrayHashes(before.Light)[0], ArrayHashes(before.Detail)[0] };
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(fixture.ManifestPath));
            manifest.tiles[0].palettes[0] = "#010203";
            manifest.tiles[0].heightRange = 0.5f;
            File.WriteAllText(fixture.ManifestPath, JsonUtility.ToJson(manifest), new UTF8Encoding(false));
            ImportSource(fixture.ManifestPath);
            var changed = TerrainArrayAssets.LoadRelightRequired(fixture.RelightPath);
            Assert.AreEqual(new Color(1, 2, 3, 255), changed.Ramp.GetPixels()[0]);
            Assert.AreEqual(0.5f, changed.Ramp.GetPixels()[80].g);
            CollectionAssert.AreEqual(maps, new[] { ArrayHashes(changed.Normal)[0],
                ArrayHashes(changed.Light)[0], ArrayHashes(changed.Detail)[0] });

            var tiles = manifest.tiles.ToList();
            for (int step = 0; step < 3; step++)
            {
                string suffix = TerrainTexArrayBuilder.LadderSteps[step];
                Tile lawn = JsonUtility.FromJson<Tile>(JsonUtility.ToJson(
                    manifest.tiles.Single(t => t.name == "Grass" + suffix)));
                lawn.name = "Lawn" + suffix;
                tiles.Add(lawn);
                foreach (string map in TerrainTexArrayBuilder.RelightMapSuffixes)
                    File.Copy(fixture.Root + "/Grass" + suffix + map + ".png",
                        fixture.Root + "/" + lawn.name + map + ".png");
            }
            manifest.tiles = tiles.ToArray();
            File.WriteAllText(fixture.ManifestPath, JsonUtility.ToJson(manifest), new UTF8Encoding(false));
            AssetDatabase.Refresh(Sync); // Only sources changed; never force-import the recipe here.
            string newSource = fixture.Root + "/Lawn_normal.png";
            ChangePixel(newSource, 11);
            ImportSource(newSource);
            AssertSourcePixel(fixture.RelightPath, newSource, 34, 0, 11);
        }

        [Test]
        public void TextureImporterSettings_DoNotChangeArrayBytes()
        {
            using var fixture = new Fixture();
            string[] expected = Snapshot(fixture.DetailPath, fixture.RelightPath, false);
            foreach (string path in new[] { fixture.Root + "/Path_Hi.png", fixture.Root + "/Path_Hi_normal.png" })
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Default;
                importer.isReadable = false;
                importer.sRGBTexture = !importer.sRGBTexture;
                importer.alphaIsTransparency = !importer.alphaIsTransparency;
                importer.maxTextureSize = 32;
                importer.textureCompression = TextureImporterCompression.Compressed;
                importer.SaveAndReimport();
            }
            var metadata = Directory.GetFiles(fixture.Root, "*.png.meta")
                .ToDictionary(p => p, File.ReadAllText);
            AssetDatabase.ImportAsset(fixture.DetailPath, Sync | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(fixture.RelightPath, Sync | ImportAssetOptions.ForceUpdate);
            CollectionAssert.AreEqual(expected, Snapshot(fixture.DetailPath, fixture.RelightPath, false));
            foreach (var pair in metadata)
                Assert.AreEqual(pair.Value, File.ReadAllText(pair.Key), "Recipe import rewrote " + pair.Key);
        }

        [TestCase("Missing")]
        [TestCase("WrongSize")]
        [TestCase("LfsPointer")]
        [TestCase("CorruptPng")]
        [TestCase("SixteenBitPng")]
        [TestCase("PalettedPng")]
        public void InvalidSource_ReportsPathAndPublishesNoPartialKit(string kind)
        {
            using var fixture = new Fixture();
            string path = fixture.Root + "/Path_Hi_detail.png";
            switch (kind)
            {
                case "Missing": File.Delete(path); break;
                case "WrongSize": WriteSmallPng(path); break;
                case "LfsPointer": File.WriteAllText(path, "version https://git-lfs.github.com/spec/v1\noid sha256:" +
                    new string('0', 64) + "\nsize 100\n"); break;
                case "CorruptPng": File.WriteAllBytes(path, File.ReadAllBytes(path).Take(40).ToArray()); break;
                // Only IHDR is needed to test refusal before decoding; repair its CRC too.
                case "SixteenBitPng": RewriteHeader(path, 16, 6); break;
                case "PalettedPng": RewriteHeader(path, 8, 3); break;
                default: Assert.Fail("Unhandled input case " + kind); break;
            }
            ExpectError(path);
            // Direct source dependency: recipe import reads the file, not its old TextureImporter artifact.
            AssetDatabase.ImportAsset(fixture.RelightPath, Sync | ImportAssetOptions.ForceUpdate);
            AssertRefused(fixture.RelightPath, path);
        }

        [TestCase("MissingManifest")]
        [TestCase("PartialLadder")]
        [TestCase("DuplicateTile")]
        [TestCase("InvalidPalette")]
        public void InvalidManifest_ReportsTileAndRefusesRelight(string kind)
        {
            using var fixture = new Fixture();
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(fixture.ManifestPath));
            string tile = "Path_Hi";
            switch (kind)
            {
                case "MissingManifest": File.Delete(fixture.ManifestPath); break;
                case "PartialLadder": manifest.tiles = manifest.tiles.Where(t => t.name != tile).ToArray(); tile = "Path"; break;
                case "DuplicateTile": manifest.tiles = manifest.tiles.Concat(new[] { manifest.tiles.Single(t => t.name == tile) }).ToArray(); break;
                case "InvalidPalette": manifest.tiles.Single(t => t.name == tile).palettes[0] = "invalid"; break;
            }
            if (kind != "MissingManifest")
                File.WriteAllText(fixture.ManifestPath, JsonUtility.ToJson(manifest), new UTF8Encoding(false));
            ExpectError(fixture.ManifestPath);
            AssetDatabase.ImportAsset(fixture.RelightPath, Sync | ImportAssetOptions.ForceUpdate);
            string error = AssertRefused(fixture.RelightPath, fixture.ManifestPath);
            if (kind != "MissingManifest") StringAssert.Contains(tile, error);
        }

        [Test]
        public void FailedReimport_CannotServeLastGoodTextures_AndRecovers()
        {
            using var fixture = new Fixture();
            string[] initial = Snapshot(fixture.DetailPath, fixture.RelightPath, true);
            byte[] manifest = File.ReadAllBytes(fixture.ManifestPath);
            File.Delete(fixture.ManifestPath);
            ExpectError(fixture.ManifestPath);
            AssetDatabase.Refresh(Sync);
            AssertRefused(fixture.RelightPath, fixture.ManifestPath);
            File.WriteAllBytes(fixture.ManifestPath, manifest);
            ImportSource(fixture.ManifestPath);
            CollectionAssert.AreEqual(initial, Snapshot(fixture.DetailPath, fixture.RelightPath, true));
        }

        static void ReimportProduction()
        {
            AssetDatabase.ImportAsset(TerrainArrayAssets.DetailRecipePath, Sync | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(TerrainArrayAssets.RelightRecipePath, Sync | ImportAssetOptions.ForceUpdate);
        }

        static void ImportSource(string path) => AssetDatabase.ImportAsset(path, Sync | ImportAssetOptions.ForceUpdate);

        static void ExpectError(string path) =>
            LogAssert.Expect(LogType.Error, new Regex(@"\[TerrainArrayImporter\].*" + Regex.Escape(path)));

        static string AssertRefused(string recipe, string source)
        {
            var error = Assert.Throws<InvalidOperationException>(() => TerrainArrayAssets.LoadRelightRequired(recipe));
            StringAssert.Contains(source, error.Message);
            // Some Unity versions keep the whole previous artifact. Accept neither partial output nor
            // a successful load of that stale artifact; the log-aware loader above must refuse it.
            int count = Textures(recipe).Length;
            Assert.IsTrue(count == 0 || count == 4, "A failed recipe published a partial relight set.");
            return error.Message;
        }

        static Texture[] Textures(string recipe) => AssetDatabase.LoadAllAssetsAtPath(recipe).OfType<Texture>().ToArray();

        static void AssertSourcePixel(string recipe, string source, int slice, int? map, int pixel)
        {
            Color32[] expected = TerrainPass9Bake.Decode(source, out _, out _);
            Texture2DArray array;
            if (map == null) array = TerrainArrayAssets.LoadDetailRequired(recipe);
            else
            {
                var set = TerrainArrayAssets.LoadRelightRequired(recipe);
                array = new[] { set.Normal, set.Light, set.Detail }[map.Value];
            }
            Assert.AreEqual(expected[pixel], array.GetPixels32(slice)[pixel], "Dependency did not update " + source);
        }

        static void ChangePixel(string path, int pixel)
        {
            Color32[] pixels = TerrainPass9Bake.Decode(path, out int width, out int height);
            pixels[pixel].r ^= 0x7f;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            try
            {
                texture.SetPixels32(pixels);
                texture.Apply(false, false);
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(texture); }
        }

        static void WriteSmallPng(string path)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try { File.WriteAllBytes(path, texture.EncodeToPNG()); }
            finally { Object.DestroyImmediate(texture); }
        }

        static void RewriteHeader(string path, byte depth, byte colourType)
        {
            byte[] bytes = File.ReadAllBytes(path);
            bytes[24] = depth; bytes[25] = colourType;
            uint crc = 0xffffffff;
            for (int i = 12; i < 29; i++)
            {
                crc ^= bytes[i];
                for (int bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0u);
            }
            crc ^= 0xffffffff;
            for (int i = 0; i < 4; i++) bytes[29 + i] = (byte)(crc >> (24 - i * 8));
            File.WriteAllBytes(path, bytes);
        }

        static string[] Snapshot(string detailPath, string relightPath, bool includeIds)
        {
            var detail = TerrainArrayAssets.LoadDetailRequired(detailPath);
            var set = TerrainArrayAssets.LoadRelightRequired(relightPath);
            var result = new List<string>();
            foreach (Texture texture in new Texture[] { detail, set.Normal, set.Light, set.Detail, set.Ramp })
            {
                result.Add(texture.name + "|" + texture.width + "|" + texture.height + "|" + texture.graphicsFormat
                    + "|" + texture.mipmapCount + "|" + texture.isReadable + "|" + texture.filterMode
                    + "|" + texture.wrapMode + "|" + texture.anisoLevel);
                if (includeIds)
                {
                    Assert.IsTrue(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(texture, out string guid, out long id));
                    result.Add(guid + ":" + id);
                }
                if (texture is Texture2DArray array) result.AddRange(ArrayHashes(array));
                else result.Add(RampHash((Texture2D)texture));
            }
            return result.ToArray();
        }

        static string[] ArrayHashes(Texture2DArray array)
        {
            var result = new string[array.mipmapCount];
            for (int mip = 0; mip < result.Length; mip++)
            {
                using var bytes = new MemoryStream();
                for (int slice = 0; slice < array.depth; slice++)
                foreach (Color32 pixel in array.GetPixels32(slice, mip))
                {
                    bytes.WriteByte(pixel.r); bytes.WriteByte(pixel.g);
                    bytes.WriteByte(pixel.b); bytes.WriteByte(pixel.a);
                }
                result[mip] = Hash(bytes.ToArray());
            }
            return result;
        }

        static string RampHash(Texture2D ramp)
        {
            using var bytes = new MemoryStream();
            foreach (Color c in ramp.GetPixels())
            foreach (float channel in new[] { c.r, c.g, c.b, c.a })
            {
                byte[] bits = BitConverter.GetBytes(channel);
                if (!BitConverter.IsLittleEndian) Array.Reverse(bits);
                bytes.Write(bits, 0, bits.Length);
            }
            return Hash(bytes.ToArray());
        }

        static string Hash(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        [Serializable] class Baseline
        {
            public string[] detailMipSha256, relightSha256;
            public string rampSha256;
        }
        [Serializable] class Manifest { public int size; public Tile[] tiles; }
        [Serializable] class Tile
        {
            public string name; public int step; public string[] palettes;
            public float heightMin, heightRange, pondMax;
        }

        sealed class Fixture : IDisposable
        {
            public readonly string Root = "Assets/__TerrainArrayImporterTests_" + Guid.NewGuid().ToString("N");
            public string DetailPath => Root + "/TerrainDetail256.hhterrain";
            public string RelightPath => Root + "/TerrainRelight.hhterrain";
            public string ManifestPath => Root + "/TerrainRelight.json";

            public Fixture()
            {
                try
                {
                    Directory.CreateDirectory(Root);
                    foreach (string material in TerrainTexArrayBuilder.Order256)
                    foreach (string step in TerrainTexArrayBuilder.LadderSteps)
                    {
                        string stem = material + step;
                        File.Copy(TerrainTexArrayBuilder.TexDir + "/" + stem + ".png", Root + "/" + stem + ".png");
                        if (material == "Lawn") continue;
                        foreach (string map in TerrainTexArrayBuilder.RelightMapSuffixes)
                            File.Copy(TerrainTexArrayBuilder.TexDir + "/" + stem + map + ".png",
                                Root + "/" + stem + map + ".png");
                    }
                    File.Copy(TerrainTexArrayBuilder.RelightJsonPath, ManifestPath);
                    File.Copy(TerrainArrayAssets.DetailRecipePath, DetailPath);
                    File.Copy(TerrainArrayAssets.RelightRecipePath, RelightPath);
                    AssetDatabase.Refresh(Sync);
                    TerrainArrayAssets.LoadDetailRequired(DetailPath);
                    TerrainArrayAssets.LoadRelightRequired(RelightPath);
                }
                catch { Dispose(); throw; }
            }

            public void Dispose()
            {
                // Unity owns fixture metadata. Restrict deletion to this exact generated Assets folder.
                string full = Path.GetFullPath(Root);
                string assets = Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar;
                if (!full.StartsWith(assets, StringComparison.OrdinalIgnoreCase)
                    || !Path.GetFileName(full).StartsWith("__TerrainArrayImporterTests_", StringComparison.Ordinal))
                    throw new InvalidOperationException("Refusing fixture cleanup outside Assets: " + full);
                if (!AssetDatabase.DeleteAsset(Root) && Directory.Exists(Root))
                {
                    Directory.Delete(full, true);
                    if (File.Exists(full + ".meta")) File.Delete(full + ".meta");
                }
            }
        }
    }
}
