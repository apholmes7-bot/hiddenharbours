using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Terrain pass 9's bake, as <see cref="TerrainKitAlbedoBytesTests"/> and <see cref="TerrainKitPass9BytesTests"/>
    /// read it. <c>docs/art/rigs/terrain/pass9/bakePass9.js</c> wrote the maps and, beside them, the manifest at
    /// <see cref="TerrainTexArrayBuilder.RelightJsonPath"/>: per tile, its palettes and parameters, and one sha256
    /// per map over the map's raw RGBA, rows top-down as the PNG stores them. The manifest also stamps the sha256
    /// of every rig file the bake ran. Those hashes are the expectations; nothing here computes one from the code
    /// under test.
    /// </summary>
    static class TerrainPass9Bake
    {
        /// <summary>The committed rig the bake ran, byte for byte (LF, pinned by <c>.gitattributes</c>).</summary>
        public const string RigDir = "docs/art/rigs/terrain/pass9";
        /// <summary>The light whose <c>unlit</c> view the bake writes as each tile's albedo.</summary>
        public const string Light = "terrainLight6.js";

        [Serializable] public sealed class Sha { public string albedo, normal, light, detail; }
        [Serializable] public sealed class Tile
        {
            public string name; public int step; public string[] palettes;
            public float heightMin, heightRange, pondMax; public Sha sha256;
        }
        [Serializable] public sealed class Manifest { public string light; public int size; public Tile[] tiles; }

        public static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        /// <summary>The manifest, its tiles by name, and its text.</summary>
        public static Manifest Load(out Dictionary<string, Tile> byName, out string json)
        {
            string path = Path.Combine(Root, TerrainTexArrayBuilder.RelightJsonPath);
            Assert.IsTrue(File.Exists(path),
                $"{TerrainTexArrayBuilder.RelightJsonPath} is missing: the bake's manifest lands with its maps.");
            json = File.ReadAllText(path);
            var m = JsonUtility.FromJson<Manifest>(json);
            Assert.IsNotNull(m?.tiles, $"{TerrainTexArrayBuilder.RelightJsonPath} did not parse.");
            byName = new Dictionary<string, Tile>();
            foreach (var t in m.tiles)
            {
                Assert.IsFalse(t?.name == null || byName.ContainsKey(t.name), $"a tile is unnamed or named twice ('{t?.name}').");
                byName.Add(t.name, t);
            }
            return m;
        }

        /// <summary>The rig files the manifest stamps: path under <see cref="RigDir"/> to sha256. JsonUtility
        /// reads no dictionary, so the object is read by pattern.</summary>
        public static Dictionary<string, string> RigStamps(string json)
        {
            var stamps = new Dictionary<string, string>();
            Match block = Regex.Match(json, @"""rigSha256""\s*:\s*\{([^}]*)\}");
            if (!block.Success) return stamps;
            foreach (Match e in Regex.Matches(block.Groups[1].Value, @"""([^""]+)""\s*:\s*""([0-9a-f]{64})"""))
                stamps[e.Groups[1].Value] = e.Groups[2].Value;
            return stamps;
        }

        public static string FileSha256(string path)
        {
            using var sha = SHA256.Create();
            return Hex(sha.ComputeHash(File.ReadAllBytes(path)));
        }

        /// <summary>The bake's hash of a map: sha256 over its raw RGBA with the rows top-down. Unity's pixels
        /// run bottom-up, so the rows are turned over first.</summary>
        public static string PixelSha256(Color32[] px, int w, int h)
        {
            Assert.AreEqual(w * h, px.Length, "the pixels are not w x h");
            var raw = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
            {
                int src = (h - 1 - y) * w, dst = y * w * 4;
                for (int x = 0; x < w; x++)
                {
                    Color32 c = px[src + x];
                    raw[dst + 4 * x] = c.r; raw[dst + 4 * x + 1] = c.g; raw[dst + 4 * x + 2] = c.b; raw[dst + 4 * x + 3] = c.a;
                }
            }
            using var sha = SHA256.Create();
            return Hex(sha.ComputeHash(raw));
        }

        /// <summary>A PNG's pixels as the file holds them, decoded on the CPU and not through its importer
        /// (CI has no graphics device).</summary>
        public static Color32[] Decode(string path, out int w, out int h)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            try
            {
                Assert.IsTrue(tex.LoadImage(File.ReadAllBytes(path), markNonReadable: false),
                    $"'{path}' did not decode (an LFS pointer not checked out?).");
                w = tex.width;
                h = tex.height;
                return tex.GetPixels32();
            }
            finally { UnityEngine.Object.DestroyImmediate(tex); }
        }

        public static bool IsSha256(string s)
        {
            if (s == null || s.Length != 64) return false;
            foreach (char c in s) if ("0123456789abcdef".IndexOf(c) < 0) return false;
            return true;
        }

        static string Hex(byte[] b)
        {
            var sb = new StringBuilder(b.Length * 2);
            foreach (byte x in b) sb.Append(x.ToString("x2"));
            return sb.ToString();
        }
    }
}
