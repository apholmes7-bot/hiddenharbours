using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests
{
    public class NineMileCreekGroundImportTests
    {
        internal static string Root
        {
            get
            {
                var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
                while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets/_Project"))) dir = dir.Parent;
                return dir?.FullName ?? throw new DirectoryNotFoundException("Run from the project directory.");
            }
        }
        internal static string Read(string path) => File.ReadAllText(Path.Combine(Root, path));
        internal static double Number(string text, string field) => double.Parse(
            Regex.Match(text, @"(?m)^\s*" + Regex.Escape(field) + @": ([\d.eE+\-]+)\s*$").Groups[1].Value,
            System.Globalization.CultureInfo.InvariantCulture);
        static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }
        static string PixelHash(ushort[] codes)
        {
            var bytes = new byte[codes.Length * 2];
            for (int i = 0; i < codes.Length; i++) { bytes[2 * i] = (byte)(codes[i] >> 8); bytes[2 * i + 1] = (byte)codes[i]; }
            return Hash(bytes);
        }

        [TestCase(NineMileCreekGround.HeightPath, "9a6c465002cb229d2853191757fb498af66cb81a635f6973f59bf95723ed9f47", "5924a6428d593bc80bc3eabe6c3887b39b05abe6aa5142b7ef9a0d6f9e8b02b0")]
        [TestCase(NineMileCreekGround.StillPath, "0046179b7e66517f79b685807357aa82f5327d70c6d48d1b8d2c566582b5406b", "2c796a61961309de81777224c5c7f089aa2d1e1d35797895808bbcb8841e0d64")]
        public void TheFilesAndEveryDecodedCodeAreTheThirdIssue(string path, string fileHash, string pixelHash)
        {
            var bytes = File.ReadAllBytes(Path.Combine(Root, path));
            Assert.That(Hash(bytes), Is.EqualTo(fileHash));
            var image = TerrainPlanPng.Read(bytes);
            Assert.That(image.Width, Is.EqualTo(1520)); Assert.That(image.Height, Is.EqualTo(1120));
            Assert.That(image.BitDepth, Is.EqualTo(16)); Assert.That(image.ColourType, Is.Zero);
            Assert.That(PixelHash(image.Samples), Is.EqualTo(pixelHash));
        }

        [Test]
        public void RuntimeFieldKeepsNorthUpRangeAndEveryCodeWithoutAnotherFill()
        {
            var image = TerrainPlanPng.ReadFile(Path.Combine(Root, NineMileCreekGround.HeightPath));
            var ground = NineMileCreekGround.Load(Root);
            for (int r = 0; r < image.Height; r++) for (int c = 0; c < image.Width; c++)
            {
                float expected = -6f + 12f * image.Samples[r * image.Width + c] / 65535f;
                Assert.That(ground.Field.ElevationAtTexel(c, image.Height - 1 - r), Is.EqualTo(expected).Within(NineMileCreekGround.CodeMetres));
            }
            // Named sites plus deterministic off-centre points; finite parity sample, not a GPU render proof.
            var points = new[] { new Vector2(-200, -8), new Vector2(98, 88), new Vector2(370, 38), new Vector2(137, 0), new Vector2(-380, -280) };
            foreach (var p in points) Assert.That(ground.ElevationAt(p), Is.EqualTo(Reference(image, p)).Within(NineMileCreekGround.CodeMetres));
            var random = new System.Random(4203);
            for (int i = 0; i < 4096; i++)
            {
                var p = new Vector2((float)random.NextDouble() * 760 - 380, (float)random.NextDouble() * 560 - 280);
                Assert.That(ground.ElevationAt(p), Is.EqualTo(Reference(image, p)).Within(NineMileCreekGround.CodeMetres));
            }
            Assert.That(ground.ElevationAt(new Vector2(-200, -8)), Is.EqualTo(6f));
        }
        static double Reference(TerrainPlanPng.Image image, Vector2 p)
        {
            double x = (p.x + 380d) * 2 - .5, y = (280d - p.y) * 2 - .5;
            int ix = (int)Math.Floor(x), iy = (int)Math.Floor(y);
            double Code(int c, int r) => image.Samples[Math.Max(0, Math.Min(1119, r)) * 1520 + Math.Max(0, Math.Min(1519, c))];
            double a = Code(ix, iy) * (1 - x + ix) + Code(ix + 1, iy) * (x - ix);
            double b = Code(ix, iy + 1) * (1 - x + ix) + Code(ix + 1, iy + 1) * (x - ix);
            return -6 + 12 * (a * (1 - y + iy) + b * (y - iy)) / 65535;
        }

        [Test]
        public void StillSentinelIsFilteredAsCodeBeforeItBecomesALevel()
        {
            Assert.That(StillWaterLevels.DecodeCode01(0, -6, 6), Is.EqualTo(StillWaterLevels.None));
            var image = TerrainPlanPng.ReadFile(Path.Combine(Root, NineMileCreekGround.StillPath));
            int rim = Array.FindIndex(image.Samples, 1, c => c != 0);
            Assert.That(rim, Is.GreaterThan(0));
            float code = image.Samples[rim] / 65535f;
            var field = new PaintedHeightField(new[] { 0f, code, 0f, code }, 2, 2, Vector2.zero, new Vector2(2, 2));
            Assert.That(StillWaterLevels.DecodeCode01(field.ElevationAt(Vector2.zero), -6, 6),
                Is.EqualTo(-6 + 6 * code).Within(NineMileCreekGround.CodeMetres));
            Assert.That(Read("Assets/_Project/Code/World/PaintedStillWater.cs"), Does.Contain("DecodeCode01(_codes.ElevationAt(worldPos)"));
        }

        [Test]
        public void EasternFreezeKeepsEveryOriginalTexel()
        {
            var before = TerrainPlanPng.ReadFile(Path.Combine(Root, "Assets/_Project/Data/Terrain/NineMileCreekSeabed_HeightTex.png"));
            var after = TerrainPlanPng.ReadFile(Path.Combine(Root, NineMileCreekGround.HeightPath));
            int count = 0;
            for (int r = 0; r < 1120; r++) for (int c = 0; c < 1520; c++)
            {
                double x = -380 + (c + .5) / 2, y = 280 - (r + .5) / 2;
                if (!(x >= 232 || x >= 170 && y <= -60)) continue;
                Assert.That(after.Samples[r * 1520 + c], Is.EqualTo(before.Samples[(r * 1520 + c) * before.Channels] * 257)); count++;
            }
            Assert.That(count, Is.EqualTo(386080));
        }

        [Test]
        public void ImportAndMapKeepTheR16Contract()
        {
            foreach (var path in new[] { NineMileCreekGround.HeightPath, NineMileCreekGround.StillPath })
            {
                string meta = Read(path + ".meta");
                foreach (var setting in new[] { "enableMipMap: 0", "sRGBTexture: 0", "isReadable: 1", "textureFormat: 9", "textureCompression: 0", "filterMode: 1", "wrapU: 1", "wrapV: 1" })
                    Assert.That(meta, Does.Contain(setting));
            }
            string map = Read(NineMileCreekGround.MapPath);
            Assert.That(map, Does.Contain("_worldSize: {x: 760, y: 560}"));
            Assert.That(Number(map, "_minElevation"), Is.EqualTo(-6)); Assert.That(Number(map, "_maxElevation"), Is.EqualTo(6));
            foreach (string path in new[] { NineMileCreekGround.HeightPath, NineMileCreekGround.StillPath })
                Assert.That(map, Does.Contain(Regex.Match(Read(path + ".meta"), @"guid: (\w+)").Groups[1].Value));
        }
    }
}
