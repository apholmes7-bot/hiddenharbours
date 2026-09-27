using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>TERRAIN PASS 9, PR 2 (2026-09-26): the arrays the ground samples are the rig's bake, byte for byte.</b>
    ///
    /// <para><see cref="TerrainTexArrayBuilder"/> packs the bake's maps (<see cref="TerrainTexArrayBuilder.TexDir"/>)
    /// into the splat shader's arrays: each tile's albedo into the detail array, its <c>_normal</c>, <c>_light</c>
    /// and <c>_detail</c> maps into the three relight arrays, and its palettes and parameters into the ramp.
    /// Between the bake and an array sit the importer and the pack, and either could move a byte: an sRGB or
    /// compressed import, alpha-as-transparency bleeding colour into the data alphas, a slice out of order, or
    /// rows turned over. So each slice is hashed as the bake hashed its map and compared with the hash the bake
    /// recorded (<see cref="TerrainPass9Bake"/>), and each ramp row with the bake's palettes and numbers. Nothing
    /// here asks the builder what to expect: the slice order is <see cref="TerrainTexArrayBuilder.Order256"/>,
    /// the contract the shader reads, and every expected byte is the rig's.</para>
    /// </summary>
    public class TerrainKitPass9BytesTests
    {
        /// <summary>The one material of the detail array the bake does not make: the kit has no Lawn. Its
        /// slices keep their albedo and carry no relight maps.</summary>
        static readonly string[] Unbaked = { "Lawn" };

        static readonly string[] RelightMaps = { "normal", "light", "detail" };

        static int Depth => TerrainTexArrayBuilder.Order256.Length * TerrainTexArrayBuilder.LadderSteps.Length;

        static string Short(string sha) => sha == null ? "(none)" : sha.Length > 12 ? sha.Substring(0, 12) : sha;

        static Texture2DArray LoadArray(string path, int size)
        {
            var a = AssetDatabase.LoadAssetAtPath<Texture2DArray>(path);
            Assert.IsNotNull(a, $"{path} is not committed: the arrays land with their maps.");
            Assert.AreEqual(Depth, a.depth, $"{path}: not one slice per material per ladder step.");
            Assert.AreEqual(size, a.width, $"{path} is not {size} px wide.");
            Assert.AreEqual(size, a.height, $"{path} is not {size} px tall.");
            Assert.IsTrue(a.isReadable, $"{path} is not readable, so its bytes cannot be checked.");
            return a;
        }

        /// <summary>Walks the detail array's slices: the slice, its tile's name, and whether the bake makes it.</summary>
        static IEnumerable<(int slice, string name, bool baked)> Slices()
        {
            for (int m = 0; m < TerrainTexArrayBuilder.Order256.Length; m++)
            for (int s = 0; s < TerrainTexArrayBuilder.LadderSteps.Length; s++)
                yield return (m * TerrainTexArrayBuilder.LadderSteps.Length + s,
                              TerrainTexArrayBuilder.Order256[m] + TerrainTexArrayBuilder.LadderSteps[s],
                              Array.IndexOf(Unbaked, TerrainTexArrayBuilder.Order256[m]) < 0);
        }

        /// <summary>The bake the arrays are compared with was made by the rig this repo commits: every rig file
        /// the manifest stamps hashes to its stamp.</summary>
        [Test]
        public void TheBake_WasMadeByTheCommittedRig()
        {
            TerrainPass9Bake.Load(out _, out string json);
            var stamps = TerrainPass9Bake.RigStamps(json);
            Assert.IsTrue(stamps.ContainsKey(TerrainPass9Bake.Light), $"the manifest stamps no {TerrainPass9Bake.Light}");
            var faults = new List<string>();
            foreach (var kv in stamps)
            {
                string path = Path.Combine(TerrainPass9Bake.Root, TerrainPass9Bake.RigDir, kv.Key);
                if (!File.Exists(path)) { faults.Add($"{kv.Key}: stamped, but not in {TerrainPass9Bake.RigDir}"); continue; }
                string sha = TerrainPass9Bake.FileSha256(path);
                if (sha != kv.Value) faults.Add($"{kv.Key}: committed {sha.Substring(0, 12)}…, the bake ran {kv.Value.Substring(0, 12)}…");
            }
            Assert.IsEmpty(faults, "The bake was not made by the committed rig:\n  " + string.Join("\n  ", faults));
        }

        [Test]
        public void TheRelightArrays_AreTheBakesMaps_ByteForByte()
        {
            var manifest = TerrainPass9Bake.Load(out var byName, out _);
            int size = manifest.size;
            var arrays = new Texture2DArray[RelightMaps.Length];
            for (int a = 0; a < arrays.Length; a++) arrays[a] = LoadArray(TerrainTexArrayBuilder.RelightArrayPaths[a], size);

            var faults = new List<string>();
            var seen = new HashSet<string>();
            int relit = 0;
            foreach (var (slice, name, baked) in Slices())
            {
                byName.TryGetValue(name, out var tile);
                if (!baked)
                {
                    if (tile != null) faults.Add($"{name}: listed as unbaked, but the bake has it.");
                    for (int a = 0; a < arrays.Length; a++)
                        foreach (Color32 c in arrays[a].GetPixels32(slice, 0))
                            if (c.r != 0 || c.g != 0 || c.b != 0 || c.a != 0)
                            {
                                faults.Add($"{name}: slice {slice} of the {RelightMaps[a]} array is not blank.");
                                break;
                            }
                    continue;
                }
                if (tile == null) { faults.Add($"{name}: slice {slice} has no tile in the bake."); continue; }
                relit++;
                for (int a = 0; a < arrays.Length; a++)
                {
                    string want = a == 0 ? tile.sha256?.normal : a == 1 ? tile.sha256?.light : tile.sha256?.detail;
                    string got = TerrainPass9Bake.PixelSha256(arrays[a].GetPixels32(slice, 0), size, size);
                    seen.Add(got);
                    if (got != want)
                        faults.Add($"{name}: slice {slice} of the {RelightMaps[a]} array is {Short(got)}…, " +
                                   $"the bake's {RelightMaps[a]} map is {Short(want)}…");
                }
            }
            Assert.IsEmpty(faults, "The relight arrays are not the bake's maps:\n  " + string.Join("\n  ", faults));
            Assert.AreEqual((TerrainTexArrayBuilder.Order256.Length - Unbaked.Length) * TerrainTexArrayBuilder.LadderSteps.Length,
                relit, "not every baked material's slices were compared");
            Assert.AreEqual(relit * RelightMaps.Length, seen.Count,
                "two relit slices hash the same: the arrays repeat a map, or the hash read no pixels");
        }

        [Test]
        public void TheDetailArray_IsTheBakesUnlit_AtEveryBakedSlice()
        {
            var manifest = TerrainPass9Bake.Load(out var byName, out _);
            int size = manifest.size;
            var array = LoadArray(TerrainTexArrayBuilder.Array256Path, size);

            var faults = new List<string>();
            foreach (var (slice, name, baked) in Slices())
            {
                string want;
                if (baked)
                {
                    if (!byName.TryGetValue(name, out var tile)) { faults.Add($"{name}: slice {slice} has no tile in the bake."); continue; }
                    want = tile.sha256?.albedo;
                }
                else
                {
                    // Not baked: the slice is the live tile's own pixels, which this PR leaves untouched.
                    string live = Path.Combine(TerrainPass9Bake.Root, TerrainTexArrayBuilder.TexDir, name + ".png");
                    Color32[] px = TerrainPass9Bake.Decode(live, out int w, out int h);
                    want = TerrainPass9Bake.PixelSha256(px, w, h);
                }
                string got = TerrainPass9Bake.PixelSha256(array.GetPixels32(slice, 0), size, size);
                if (got != want)
                    faults.Add($"{name}: slice {slice} is {Short(got)}…, the bake's albedo {Short(want)}…");
            }
            Assert.IsEmpty(faults, $"{TerrainTexArrayBuilder.Array256Path} is not the bake's albedo:\n  " + string.Join("\n  ", faults));
        }

        /// <summary>Row <c>slice</c> of the ramp holds the tile's palettes as sRGB bytes, five bands to a palette,
        /// from x = 0, then zero to x = 79, then (heightMin, heightRange, pondMax, 1) at x = 80, the layout
        /// <c>Include/TerrainLight6.hlsl</c> reads. An unbaked slice's row is zero, and its w = 0 keeps the albedo.</summary>
        [Test]
        public void TheRamp_HoldsEachTilesPalettesAndNumbers_FromTheBake()
        {
            TerrainPass9Bake.Load(out var byName, out _);
            var ramp = AssetDatabase.LoadAssetAtPath<Texture2D>(TerrainTexArrayBuilder.RelightRampPath);
            Assert.IsNotNull(ramp, $"{TerrainTexArrayBuilder.RelightRampPath} is not committed.");
            const int width = TerrainTexArrayBuilder.RelightPalettes * TerrainTexArrayBuilder.RelightBands + 1;
            Assert.AreEqual(width, ramp.width, "the ramp is not 81 texels wide");
            Assert.AreEqual(Depth, ramp.height, "the ramp is not one row per slice");
            Assert.IsTrue(ramp.isReadable, "the ramp is not readable, so its numbers cannot be checked");
            Color[] px = ramp.GetPixels();

            var faults = new List<string>();
            foreach (var (slice, name, baked) in Slices())
            {
                var want = new Color[width];
                if (baked)
                {
                    if (!byName.TryGetValue(name, out var tile)) { faults.Add($"{name}: no tile in the bake."); continue; }
                    for (int i = 0; i < tile.palettes.Length; i++)
                    {
                        int rgb = Convert.ToInt32(tile.palettes[i].Substring(1), 16);
                        want[i] = new Color((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255, 255);
                    }
                    want[width - 1] = new Color(tile.heightMin, tile.heightRange, tile.pondMax, 1f);
                }
                for (int x = 0; x < width; x++)
                {
                    Color got = px[slice * width + x], w = want[x];
                    // Exactly: Color's == forgives 1e-5, and a ramp is either the bake's numbers or not.
                    if (got.r != w.r || got.g != w.g || got.b != w.b || got.a != w.a)
                    {
                        faults.Add($"{name}: row {slice}, x {x} holds {got}, the bake's is {want[x]}");
                        break;
                    }
                }
            }
            Assert.IsEmpty(faults, "The ramp is not the bake's palettes and numbers:\n  " + string.Join("\n  ", faults));
        }
    }
}
