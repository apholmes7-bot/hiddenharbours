using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// NMC H9: the fuel stop kit (pass 6) comes into the repo unchanged, on the owner's word, as PR 999 brought the
    /// set-pieces kit: the drop's <c>hh-nmc-key-scenes/fuel-stop/</c> filed under its own folder name. The four files
    /// are pinned by the size and sha256 of their LF bytes (the drop's bytes are LF; a box with core.autocrlf checks
    /// text out with CRLF and CI checks it out LF), the folder holds nothing else, and no file is an LFS pointer.
    /// File reads only: no rig host, no editor.
    /// </summary>
    public class FuelStopKitIntakeTests
    {
        const string Kit = "docs/art/rigs/fuel-stop/";
        static readonly (string File, long LfBytes, string Sha256Lf)[] Delivered =
        {
            ("fuelStopRig.js", 61538, "1fc476b3a5e8d7ed1099c2680a7db931e0bf91d444ade8cb5a64134b74839e25"),
            ("nmcFuelStop.js", 38753, "f111bb6ff419a21b497e3558d5992604a7e20249f9d07b0ce9a86cc5e1ea7ccb"),
            ("nfsBake2c.js", 3932, "22c36af750d21bd970abf5a2faf9ce1e90132841636d86144302edb7b2a3c31e"),
            ("nfsBakePage2c.html", 2740, "58185806bea373555181110f9988a2acfcbb7828e63a687860d300b094a8d67d"),
        };

        /// <summary>The file's bytes with each CR that ends a line dropped, so a CRLF checkout hashes as the LF drop.</summary>
        static byte[] LfBytes(string path)
        {
            byte[] raw = File.ReadAllBytes(path);
            var lf = new List<byte>(raw.Length);
            for (int i = 0; i < raw.Length; i++)
                if (!(raw[i] == (byte)'\r' && i + 1 < raw.Length && raw[i + 1] == (byte)'\n')) lf.Add(raw[i]);
            return lf.ToArray();
        }

        static string Sha256Hex(byte[] bytes)
        {
            using (var sha = SHA256.Create())
                return string.Concat(sha.ComputeHash(bytes).Select(b => b.ToString("x2")));
        }

        [Test]
        public void TheKitIsTheFourDeliveredFiles_ByteForByteAtLf()
        {
            Assert.That(Directory.Exists(Kit), Is.True, Kit + " is not in the repo");
            string prefix = Path.GetFullPath(Kit);
            var files = Directory.GetFiles(prefix, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetFullPath(f).Substring(prefix.Length).TrimStart('\\', '/').Replace('\\', '/'));
            Assert.That(files, Is.EquivalentTo(Delivered.Select(d => d.File)), "the kit folder holds other files");
            foreach (var pin in Delivered)
            {
                byte[] lf = LfBytes(Kit + pin.File);
                Assert.That(lf.LongLength, Is.EqualTo(pin.LfBytes), pin.File + " size at LF");
                Assert.That(Sha256Hex(lf), Is.EqualTo(pin.Sha256Lf), pin.File + " sha256 at LF");
            }
        }

        [Test]
        public void NoKitFileIsAnLfsPointer_AndEachIsUtf8Text()
        {
            foreach (var pin in Delivered)
            {
                byte[] lf = LfBytes(Kit + pin.File);
                string head = Encoding.ASCII.GetString(lf, 0, Math.Min(lf.Length, 64));
                Assert.That(head, Does.Not.StartWith("version https://git-lfs"), pin.File + " is an LFS pointer");
                Assert.That(lf, Has.No.Member((byte)0), pin.File + " carries a NUL byte");
                Assert.DoesNotThrow(() => new UTF8Encoding(false, true).GetString(lf), pin.File + " is not UTF-8");
            }
        }
    }
}
