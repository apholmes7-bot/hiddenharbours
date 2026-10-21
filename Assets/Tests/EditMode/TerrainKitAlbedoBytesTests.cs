using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>TERRAIN PASS 9, PR 2 (2026-09-26): the live terrain albedo IS the light's unlit, as the rig baked it.</b>
    ///
    /// <para>The px flip (PR 1 of 3, 2026-09-17) copied each px material's albedo from the greenery kit's
    /// bakes, and this test proved that copy. Terrain pass 9 re-bakes the albedo instead:
    /// <c>docs/art/rigs/terrain/pass9/bakePass9.js</c> runs the kit's TerrainLight6 on Node and writes each
    /// tile's albedo as the light's <c>unlit</c> view, beside the tile's relight maps. The PNGs and the bake's
    /// manifest are copied into <see cref="TerrainTexArrayBuilder.TexDir"/> as the bake wrote them, so the
    /// albedo the arrays pack and the maps the relight reads are one bake. This proves the copy and keeps
    /// proving it: a tile re-saved by an image editor, "fixed" by hand, left over from the flip, or re-baked
    /// from a rig this repo does not commit reads red here, by file name.</para>
    ///
    /// <para>The expectations are the rig's. The manifest holds each tile's albedo hash (raw RGBA, rows
    /// top-down: <see cref="TerrainPass9Bake"/>), names <see cref="TerrainPass9Bake.Light"/> as its light, and
    /// stamps that light's sha256, which must be the bytes committed in <see cref="TerrainPass9Bake.RigDir"/>.
    /// Each live PNG is decoded as the file holds it, not through its importer, and hashed the same way.</para>
    ///
    /// <para>The list is the bake's, written out: what PR 2 claims to have baked, not what the array builder
    /// happens to pack. It is every material whose <c>"px"</c> is true in <c>materials.json</c>: the flip's
    /// twenty, and Path. Lawn and Sandstone are NOT here: the kit has neither, and this PR leaves both
    /// untouched. Path is new with terrain pass 9; its three metas are Dirt's import settings under fresh
    /// guids, as Mud's were.</para>
    /// </summary>
    public class TerrainKitAlbedoBytesTests
    {
        /// <summary>Every material the bake re-baked, all three ladder steps each.</summary>
        static readonly string[] Baked =
        {
            "Grass", "Marram", "Sand", "Shelf", "Dirt", "Marsh", "Sedge", "Ledge", "Rockweed",
            "Eelgrass", "Irishmoss",
            "Bank",   // a face material: in no array and drawn by nothing yet, but the bake makes it
            "Shingle", "Ripple", "Silt", "Foreshore", "Talus", "Musselbed", "Oysterreef",
            "Mud",    // new with the px kit (owner ruling M1): splat index 19, E.a
            "Path",   // new with terrain pass 9 (2026-09-25): splat index 20, F.r
        };

        static readonly string[] Steps = { "_Lo", "", "_Hi" };

        [Test]
        public void LiveAlbedo_IsTheLightsUnlit_ForEveryBakedMaterial()
        {
            var manifest = TerrainPass9Bake.Load(out var byName, out string json);
            Assert.AreEqual(TerrainPass9Bake.Light, manifest.light,
                $"the bake's albedo is not {TerrainPass9Bake.Light}'s unlit");
            TerrainPass9Bake.RigStamps(json).TryGetValue(TerrainPass9Bake.Light, out string stamped);
            string rig = Path.Combine(TerrainPass9Bake.Root, TerrainPass9Bake.RigDir, TerrainPass9Bake.Light);
            Assert.IsTrue(File.Exists(rig), $"{TerrainPass9Bake.RigDir}/{TerrainPass9Bake.Light} is missing");
            Assert.AreEqual(TerrainPass9Bake.FileSha256(rig), stamped,
                $"the bake ran a {TerrainPass9Bake.Light} that is not the one committed in {TerrainPass9Bake.RigDir}");

            string root = TerrainPass9Bake.Root;
            var faults = new List<string>();
            var liveHashes = new Dictionary<string, string>();

            foreach (string name in Baked)
            foreach (string step in Steps)
            {
                string file = name + step + ".png";
                string live = Path.Combine(root, TerrainTexArrayBuilder.TexDir, file);

                if (!byName.TryGetValue(name + step, out var tile))
                {
                    faults.Add($"{file}: the bake has no tile '{name + step}'.");
                    continue;
                }
                if (!File.Exists(live))
                {
                    faults.Add($"{file}: not live in {TerrainTexArrayBuilder.TexDir}.");
                    continue;
                }
                if (!File.Exists(live + ".meta"))
                    faults.Add($"{file}: the live PNG has no .meta, and a regenerated one moves the guid the " +
                               "arrays and scenes resolve.");

                Color32[] px = TerrainPass9Bake.Decode(live, out int w, out int h);
                if (w != manifest.size || h != manifest.size)
                {
                    faults.Add($"{file}: {w} x {h} px, not the bake's {manifest.size}.");
                    continue;
                }
                string liveSha = TerrainPass9Bake.PixelSha256(px, w, h);
                liveHashes[file] = liveSha;
                string bakeSha = tile.sha256?.albedo ?? "";
                if (liveSha != bakeSha)
                    faults.Add($"{file}: live pixels {liveSha.Substring(0, 12)}… are not the bake's " +
                               $"{(bakeSha.Length >= 12 ? bakeSha.Substring(0, 12) : "(none)")}…");
            }

            Assert.IsEmpty(faults,
                $"The live terrain albedo is not the light's unlit as the rig baked it ({faults.Count} fault(s)):\n  " +
                string.Join("\n  ", faults) +
                "\nCopy the tile from the bake again (node docs/art/rigs/terrain/pass9/bakePass9.js); never " +
                "hand-edit or 'fix' a tile here. A fault in a tile is a written finding for the rig's author.");

            // SABOTAGE PROOF: the comparison above is only as good as the hashes on both sides. Real pixels give
            // 64 hex characters, and the bake's tiles are all different images, so no two live hashes are equal;
            // a manifest of copied tiles, or a helper that read no pixels, would make them so.
            foreach (var kv in liveHashes)
                Assert.IsTrue(TerrainPass9Bake.IsSha256(kv.Value),
                    $"{kv.Key}: '{kv.Value}' is not a sha256, so the guard above compared nothing.");
            Assert.AreEqual(Baked.Length * Steps.Length, liveHashes.Values.Distinct().Count(),
                "Two live tiles hash the same: either the bake made a duplicate or the hash helper is not " +
                "reading the pixels.");
        }
    }
}
