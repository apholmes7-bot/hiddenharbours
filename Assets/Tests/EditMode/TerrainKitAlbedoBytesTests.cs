using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE PX FLIP (PR 1 of 3, 2026-09-17): the live terrain albedo IS the kit's bytes.</b>
    ///
    /// <para>The greenery px kit ships BAKES, and its bakers are absent by design: the PNGs are the
    /// artifact. So the flip is a byte copy from <see cref="KitDir"/> over the files in
    /// <see cref="TerrainTexArrayBuilder.TexDir"/>, keeping every existing <c>.meta</c> (the import
    /// settings and the guids stay). This proves the copy and keeps proving it: a tile re-saved by an
    /// image editor, "fixed" by hand, or re-baked from a rig this repo does not have reads red here,
    /// by file name, instead of drifting quietly away from the kit it claims to be.</para>
    ///
    /// <para>No hash is restated: both sides are hashed out of the checkout. A file still in its Git
    /// LFS pointer form is compared by the pointer's own <c>oid sha256</c>, which IS the hash of the
    /// content it stands for, so a checkout that did not smudge one side verifies truthfully instead
    /// of comparing a 130-byte pointer against a PNG.</para>
    ///
    /// <para>The list is the flip's, written out: what PR 1 claims to have copied, not what the array
    /// builder happens to pack (a guard that asks the code for its list is a mirror). Lawn and
    /// Sandstone are NOT here: the kit has no Lawn, and PR 1 leaves both untouched. Mud IS here
    /// although nothing drew it before: the flip adds it (owner ruling M1), and because the kit
    /// ships no metas, its three are Dirt's import settings under fresh guids.</para>
    /// </summary>
    public class TerrainKitAlbedoBytesTests
    {
        const string KitDir = "docs/art/rigs/px-greenery-harmony-kit/Art/Textures/TerrainPx";
        const string LfsPointerPrefix = "version https://git-lfs";
        const string LfsOidPrefix = "oid sha256:";

        /// <summary>Every material whose albedo the flip replaced, all three ladder steps each.</summary>
        static readonly string[] Flipped =
        {
            "Grass", "Marram", "Sand", "Shelf", "Dirt", "Marsh", "Sedge", "Ledge", "Rockweed",
            "Eelgrass", "Irishmoss",
            "Bank",   // a face material: in no array and drawn by nothing yet, but it is the kit's
            // The seven that left the retired 512 array for the 256 array (owner ruling A2): the kit
            // ships them at 256 px / 8 m, so their live PNGs changed size as well as bytes.
            "Shingle", "Ripple", "Silt", "Foreshore", "Talus", "Musselbed", "Oysterreef",
            "Mud",    // new with the kit (owner ruling M1): splat index 19, E.a
        };

        static readonly string[] Steps = { "_Lo", "", "_Hi" };

        [Test]
        public void LiveAlbedo_IsTheKitsBytes_ForEveryFlippedMaterial()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var faults = new List<string>();
            var kitHashes = new Dictionary<string, string>();

            foreach (string name in Flipped)
            foreach (string step in Steps)
            {
                string file = name + step + ".png";
                string kit = Path.Combine(root, KitDir, file);
                string live = Path.Combine(root, TerrainTexArrayBuilder.TexDir, file);

                if (!File.Exists(kit)) { faults.Add($"{file}: not in the kit ({KitDir})."); continue; }
                if (!File.Exists(live))
                {
                    faults.Add($"{file}: not live in {TerrainTexArrayBuilder.TexDir}.");
                    continue;
                }
                if (!File.Exists(live + ".meta"))
                    faults.Add($"{file}: the live PNG has no .meta — the flip keeps every existing meta, " +
                               "and a regenerated one moves the guid the arrays and scenes resolve.");

                string kitSha = ContentSha256(kit);
                string liveSha = ContentSha256(live);
                kitHashes[file] = kitSha;
                if (kitSha != liveSha)
                    faults.Add($"{file}: live {liveSha.Substring(0, 12)}… is not the kit's " +
                               $"{kitSha.Substring(0, 12)}…");
            }

            Assert.IsEmpty(faults,
                $"The live terrain albedo is not the kit's bytes ({faults.Count} fault(s)):\n  " +
                string.Join("\n  ", faults) +
                "\nCopy the file from the kit again; never re-bake, hand-edit or 'fix' a tile here — a " +
                "fault in a tile is a written finding for the kit's author.");

            // SABOTAGE PROOF: a hash helper that returned one constant (an empty pointer parse, say)
            // would pass the comparison above for every file. Real content gives 64 hex characters,
            // and the kit's files are all different images.
            foreach (var kv in kitHashes)
                Assert.IsTrue(kv.Value.Length == 64 && kv.Value.All(c => "0123456789abcdef".IndexOf(c) >= 0),
                    $"{kv.Key}: '{kv.Value}' is not a sha256 — the guard above compared nothing.");
            Assert.AreEqual(Flipped.Length * Steps.Length, kitHashes.Values.Distinct().Count(),
                "Two kit tiles hash the same — either the kit shipped a duplicate or the hash helper " +
                "is not reading the files.");
        }

        /// <summary>The sha256 of the content a file carries: the pointer's own oid for a Git LFS
        /// pointer, otherwise the hash of the bytes on disk.</summary>
        static string ContentSha256(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length < 1024)
            {
                string text = Encoding.ASCII.GetString(bytes);
                if (text.StartsWith(LfsPointerPrefix))
                {
                    foreach (string line in text.Split('\n'))
                        if (line.StartsWith(LfsOidPrefix))
                            return line.Substring(LfsOidPrefix.Length).Trim();
                    return "";   // a pointer with no oid: the length check above reports it
                }
            }

            using var sha = SHA256.Create();
            var sb = new StringBuilder(64);
            foreach (byte x in sha.ComputeHash(bytes)) sb.Append(x.ToString("x2"));
            return sb.ToString();
        }
    }
}
