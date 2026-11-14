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
    /// NMC H7: the four kit files Water Street's rows name (NmcMainStreet, NmcCreekHouses, NmcStores, NmcShoreEstates)
    /// come into the repo unchanged, on the owner's word, as PR 999 brought the set-pieces kit and H9 the fuel stop kit:
    /// each file under the drop's own folder name (<c>hh-nmc-key-scenes/main-street/</c> and its three siblings, the
    /// one-scene package's third issue, 10-07). Each file is pinned by the size and sha256 of its LF bytes (the drop's
    /// bytes are LF; a box with core.autocrlf checks text out with CRLF and CI checks it out LF) and by the revision it
    /// declares, each folder holds nothing else, and no file is an LFS pointer. The rows that wait on the four are
    /// counted from the H7 row fixture. File reads only: no rig host, no editor.
    /// <para>The yes named these four files and no other. NmcCreekHouses, NmcShoreEstates and NmcMainStreet refuse to
    /// load without the Shore Lane kit, which is not in the repo, so nothing here draws a pixel yet.</para>
    /// </summary>
    public class NmcFourKitsIntakeTests
    {
        const string Rigs = "docs/art/rigs/";
        const string RowFixture = "Assets/Tests/EditMode/Fixtures/NmcH7Rows.tsv";

        static readonly (string Kit, string Folder, string File, long LfBytes, string Sha256Lf, string Revision, int MainStreetRows)[] Delivered =
        {
            ("NmcMainStreet", "main-street/", "nmcMainStreet.js", 60693,
                "da85e4557b0fc5388be06959449c6ee797a053da7cad692af56b2d7b2a8f4941", "1.0 (main street redrawn, 10-05)", 54),
            ("NmcCreekHouses", "north-creek/", "nmcCreekHouses.js", 46927,
                "e77506ca97083142c1656a7a1b0aa59935e5bc2699a9871daff034b21644abc3", "1.0 (the north creek redrawn, 10-02)", 5),
            ("NmcStores", "plaza-stores/", "nmcStores.js", 92357,
                "823ce694e88354c29183bf2f1ac613bc5cb17ac2066ad05ec074601f576452c8", "1.1 (10-06: the side docks)", 2),
            ("NmcShoreEstates", "south-shore/", "nmcShoreEstates.js", 37867,
                "e9c245ba748caa6d5e4bad97657f64bb816cb2cdd14d143f99071a46292dc384", "1.0 (the south shore redrawn, 10-04)", 1),
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
        public void EachKitFolderHoldsItsOneDeliveredFile_ByteForByteAtLf()
        {
            foreach (var pin in Delivered)
            {
                string kit = Rigs + pin.Folder;
                Assert.That(Directory.Exists(kit), Is.True, kit + " is not in the repo");
                string prefix = Path.GetFullPath(kit);
                var files = Directory.GetFiles(prefix, "*", SearchOption.AllDirectories)
                    .Select(f => Path.GetFullPath(f).Substring(prefix.Length).TrimStart('\\', '/').Replace('\\', '/'));
                Assert.That(files, Is.EquivalentTo(new[] { pin.File }), kit + " holds other files");
                byte[] lf = LfBytes(kit + pin.File);
                Assert.That(lf.LongLength, Is.EqualTo(pin.LfBytes), pin.File + " size at LF");
                Assert.That(Sha256Hex(lf), Is.EqualTo(pin.Sha256Lf), pin.File + " sha256 at LF");
            }
        }

        [Test]
        public void NoKitFileIsAnLfsPointer_AndEachIsUtf8Text()
        {
            foreach (var pin in Delivered)
            {
                byte[] lf = LfBytes(Rigs + pin.Folder + pin.File);
                string head = Encoding.ASCII.GetString(lf, 0, Math.Min(lf.Length, 64));
                Assert.That(head, Does.Not.StartWith("version https://git-lfs"), pin.File + " is an LFS pointer");
                Assert.That(lf, Has.No.Member((byte)0), pin.File + " carries a NUL byte");
                Assert.DoesNotThrow(() => new UTF8Encoding(false, true).GetString(lf), pin.File + " is not UTF-8");
            }
        }

        [Test]
        public void EachFileDeclaresItsKitAndItsDeliveredRevision()
        {
            foreach (var pin in Delivered)
            {
                string text = Encoding.UTF8.GetString(LfBytes(Rigs + pin.Folder + pin.File));
                Assert.That(text, Does.Contain("globalThis." + pin.Kit + ")"), pin.File + " does not name its global");
                Assert.That(text, Does.Contain("root." + pin.Kit + " = "), pin.File + " does not publish " + pin.Kit);
                Assert.That(text, Does.Contain("revision: '" + pin.Revision + "'"), pin.File + " revision");
            }
        }

        [Test]
        public void WaterStreetsRowsOnTheFourKits_AreTheSixtyTwoTheIntakeServes()
        {
            var rows = File.ReadAllLines(RowFixture).Skip(1).Where(l => l.Length > 0).Select(l => l.Split('\t')).ToArray();
            foreach (var pin in Delivered)
            {
                var onKit = rows.Where(r => r[3] == pin.Kit).ToArray();
                Assert.That(onKit.Length, Is.EqualTo(pin.MainStreetRows), pin.Kit + " rows in the H7 fixture");
                Assert.That(onKit.Select(r => r[0]).Distinct(), Is.EquivalentTo(new[] { "main_street" }), pin.Kit + " rows' scene");
            }
            Assert.That(Delivered.Sum(d => d.MainStreetRows), Is.EqualTo(62));
        }
    }
}
