using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>THE GREENERY PX KIT (owner drop 2026-09-11) — the intake guards.</b>
    ///
    /// <para>A pixel terrain vocabulary (21 materials × 3 lighting steps × 5 channels, plus 17 edge
    /// strips in 12 orientations), the greenery families that stand on it, eight rigs and two
    /// gameplay sidecars. It lands under <c>docs/</c>, so there are no <c>.meta</c> files and no
    /// importer decisions here: this PR retires nothing and wires nothing. The splat shader, the
    /// terrain catalogs and the live <c>docs/art/rigs/*.js</c> are untouched.</para>
    ///
    /// <para>What these guards exist to prevent is the failure this fleet keeps shipping: art that
    /// arrives, is believed, and is discovered months later to disagree with its own manifest. So
    /// every number below is READ from the landed files and checked against the landed bytes — no
    /// hash is restated here, and no census is copied from a README. Where the drop contradicts
    /// itself the contradiction is RECORDED (see <see cref="TheEdgesManifestDeclaresABlendChannelThatShipsOnNoStrip"/>
    /// and <see cref="TheKitReadmeMiscountsItsOwnTerrainPxDirectory"/>) rather than quietly
    /// corrected: a supplier's manifest stops being evidence the moment we edit it to suit us.</para>
    ///
    /// <para>Pure file and CPU work — PNG sizes come out of the IHDR chunk, 24 bytes a file, so the
    /// 1,131-texture sweep needs no graphics device and runs on CI's headless editor.</para>
    /// </summary>
    public class GreeneryPxKitTests
    {
        const string Kit = "docs/art/rigs/px-greenery-harmony-kit";

        /// <summary>The rig this repo actually bakes from, which the drop's copy must NOT replace.</summary>
        const string LiveShorePlantRig = "docs/art/rigs/shorePlantRig.js";

        const string LfsPointerPrefix = "version https://git-lfs";

        const char FwdSlash = '/';
        static readonly string Bullet = "\n   ";

        /// <summary>
        /// Load order is a CONTRACT, not a convenience: the kit's rigs are chained by CONCATENATION
        /// into one global scope, and <c>pxKit.js</c> throws
        /// <c>'Art/pixelLanguage.js must be loaded first'</c> if the language is not already there.
        /// Each entry is (kit-relative file, the global it must publish).
        /// </summary>
        static readonly (string File, string Global)[] Chain =
        {
            ("Art/pixelLanguage.js", "PxLang"),
            ("Art/pxKit.js", "PxKit"),
            ("Art/pxKit2.js", "PxKit2"),
            ("Art/flowerRig.js", "Flowers"),
            ("Art/grassSpeciesRig.js", "GrassSpeciesRig"),
            ("Art/grassTuftRig.js", "GrassTuftRig"),
            ("Art/shorePlantRig.js", "ShorePlants"),
            ("Art/driftWeedRig.js", "DriftWeed"),
            ("scene/harmonyScenes.js", "Harmony"),
        };

        /// <summary>
        /// Per-directory file counts, NON-recursive, as landed. Written down here because a census
        /// recomputed from the directory it is checking proves nothing.
        /// </summary>
        static readonly (string Dir, int Files)[] Census =
        {
            ("", 2),                               // README.md + SHA256SUMS.txt
            ("Art", 9),                            // 8 rigs + shorePlantRig.contract.json
            ("Art/gameplay", 2),
            ("Art/Foliage/Flowers", 38),
            ("Art/Foliage/Shrubs", 5),
            ("Art/Sprites/Grass", 31),
            ("Art/Sprites/Scatter", 10),
            ("Art/Sprites/Shore/Drift", 5),
            ("Art/Textures/TerrainPx", 357),       // 355 PNG + TerrainPx.json + README.md
            ("Art/Textures/TerrainPx/Edges", 777), // 776 PNG + Edges.json
            ("gallery/harmony", 24),
            ("scene", 1),
        };

        const int TotalFiles = 1261;

        /// <summary>The four materials that ship extra <c>_v2</c>/<c>_v3</c> tiles, and only they.</summary>
        static readonly string[] VariantMaterials = { "Grass", "Dirt", "Sand", "Shingle" };

        /// <summary>Lighting steps. The empty string is the key step — the albedo the palette is cut at.</summary>
        static readonly string[] Steps = { "", "_Hi", "_Lo" };

        /// <summary>Channel suffixes. Albedo carries NO suffix, which is why the list starts empty.</summary>
        static readonly string[] TerrainChannels = { "", "_unlit", "_mask", "_normal", "_blend" };

        /// <summary>Edges ship four channels, not five — see the erratum guard below.</summary>
        static readonly string[] EdgeChannels = { "", "_mask", "_normal", "_unlit" };

        IRigScriptHost _host;
        string _chainFailure;

        // =========================================================================================
        // Helpers
        // =========================================================================================

        static string Full(string repoRelative) => Path.Combine(RigCatalog.RepoRoot, repoRelative);

        static string InKit(string kitRelative) =>
            Full(kitRelative.Length == 0 ? Kit : Kit + "/" + kitRelative);

        static string Sha256Hex(byte[] b)
        {
            using var sha = SHA256.Create();
            var sb = new StringBuilder(64);
            foreach (byte x in sha.ComputeHash(b)) sb.Append(x.ToString("x2"));
            return sb.ToString();
        }

        /// <summary>
        /// The bytes git stores in the blob: CRLF collapsed to LF, nothing else touched. The kit's
        /// own <c>SHA256SUMS.txt</c> documents this recipe in its trailing comment block, and the two
        /// sidecars' <c>derivedFrom</c> stamps are over the same form — so a checkout that normalises
        /// line endings on the way out still verifies.
        /// </summary>
        static byte[] NormaliseLf(byte[] raw)
        {
            var buffer = new List<byte>(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                if (raw[i] == 0x0D && i + 1 < raw.Length && raw[i + 1] == 0x0A) continue;
                buffer.Add(raw[i]);
            }
            return buffer.ToArray();
        }

        static string LfSha(string repoRelative)
        {
            string abs = Full(repoRelative);
            Assert.IsTrue(File.Exists(abs), $"'{repoRelative}' is not in the checkout.");
            return Sha256Hex(NormaliseLf(File.ReadAllBytes(abs)));
        }

        static object Json(string kitRelative)
        {
            string abs = InKit(kitRelative);
            Assert.IsTrue(File.Exists(abs), $"'{Kit}/{kitRelative}' is not in the checkout.");
            object node = MiniJson.Parse(File.ReadAllText(abs, Encoding.UTF8));
            Assert.IsNotNull(node, $"'{Kit}/{kitRelative}' did not parse as JSON.");
            return node;
        }

        /// <summary>A PNG's declared size, straight out of the IHDR chunk — no decode, no GPU.</summary>
        static (int W, int H) PngSize(string absolutePath)
        {
            byte[] png = File.ReadAllBytes(absolutePath);
            if (png.Length < 200 &&
                Encoding.ASCII.GetString(png, 0, Math.Min(png.Length, LfsPointerPrefix.Length))
                        .StartsWith(LfsPointerPrefix, StringComparison.Ordinal))
                Assert.Fail(
                    $"'{absolutePath}' is still a Git LFS POINTER, not a PNG. The kit's 1,237 textures are " +
                    "committed through LFS — run `git lfs pull`. A checkout without its art is not a passing " +
                    "state, it is a checkout that cannot see what it is asserting about.");

            Assert.Greater(png.Length, 24, $"'{absolutePath}' is too short to be a PNG.");
            int Be32(int at) => (png[at] << 24) | (png[at + 1] << 16) | (png[at + 2] << 8) | png[at + 3];
            return (Be32(16), Be32(20));
        }

        static string[] FilesIn(string kitRelative) =>
            Directory.GetFiles(InKit(kitRelative))
                     .Select(Path.GetFileName)
                     .OrderBy(n => n, StringComparer.Ordinal)
                     .ToArray();

        static string[] PngsIn(string kitRelative) =>
            FilesIn(kitRelative).Where(n => n.EndsWith(".png", StringComparison.Ordinal)).ToArray();

        /// <summary>A material stem is the leading run of letters: "Grass_v2_blend.png" → "Grass".</summary>
        static string Stem(string fileName) => new string(fileName.TakeWhile(char.IsLetter).ToArray());

        static Dictionary<string, object> Dict(object node, string key)
        {
            var d = MiniJson.Dict(node, key);
            Assert.IsNotNull(d, $"expected an object under '{key}'.");
            return d;
        }

        static double Num(object node, string key)
        {
            var d = node as Dictionary<string, object>;
            Assert.IsNotNull(d, $"expected an object when reading '{key}'.");
            Assert.IsTrue(d.ContainsKey(key), $"no '{key}' in this block.");
            return Convert.ToDouble(d[key], CultureInfo.InvariantCulture);
        }

        static int IntAt(List<object> list, int index) =>
            Convert.ToInt32(list[index], CultureInfo.InvariantCulture);

        /// <summary>
        /// Structural equality over two MiniJson trees. <c>path</c> is threaded through so a failure
        /// names the field that moved rather than dumping two manifests at the reader.
        /// </summary>
        static bool DeepEquals(object a, object b, string path, out string where)
        {
            where = path;
            if (a is Dictionary<string, object> da && b is Dictionary<string, object> db)
            {
                if (da.Count != db.Count) { where = $"{path}: {da.Count} keys vs {db.Count}"; return false; }
                foreach (var kv in da)
                {
                    if (!db.TryGetValue(kv.Key, out object other)) { where = $"{path}.{kv.Key} is missing"; return false; }
                    if (!DeepEquals(kv.Value, other, path + "." + kv.Key, out where)) return false;
                }
                return true;
            }
            if (a is List<object> la && b is List<object> lb)
            {
                if (la.Count != lb.Count) { where = $"{path}: {la.Count} items vs {lb.Count}"; return false; }
                for (int i = 0; i < la.Count; i++)
                    if (!DeepEquals(la[i], lb[i], $"{path}[{i}]", out where)) return false;
                return true;
            }
            if (a == null || b == null)
            {
                if (a == null && b == null) return true;
                where = $"{path}: {(a == null ? "null" : a.ToString())} vs {(b == null ? "null" : b.ToString())}";
                return false;
            }
            if (a is bool || b is bool)
            {
                if (Equals(a, b)) return true;
                where = $"{path}: {a} vs {b}";
                return false;
            }
            if (a is string || b is string)
            {
                if (Equals(a, b)) return true;
                where = $"{path}: '{a}' vs '{b}'";
                return false;
            }

            double na = Convert.ToDouble(a, CultureInfo.InvariantCulture);
            double nb = Convert.ToDouble(b, CultureInfo.InvariantCulture);
            if (Math.Abs(na - nb) <= 1e-12) return true;
            where = $"{path}: {na} vs {nb}";
            return false;
        }

        // =========================================================================================

        [OneTimeSetUp]
        public void LoadTheRigChain()
        {
            _host = RigScriptHostFactory.Create();
            foreach (var entry in Chain)
            {
                string abs = InKit(entry.File);
                if (!File.Exists(abs)) { _chainFailure = $"{entry.File} is not in the kit."; return; }
                try
                {
                    _host.Execute(File.ReadAllText(abs, Encoding.UTF8));
                }
                catch (Exception ex)
                {
                    _chainFailure = $"{entry.File} threw on evaluation: {ex.Message}";
                    return;
                }
            }
        }

        [OneTimeTearDown]
        public void ReleaseTheHost() => _host?.Dispose();

        // =========================================================================================
        // Gate 1 — the manifest, checked against the bytes that landed
        // =========================================================================================

        /// <summary>
        /// All eighteen entries of the kit's own <c>SHA256SUMS.txt</c>, recomputed over the LANDED
        /// files. The expected hashes are read out of the manifest — never restated here — so this
        /// guard cannot be satisfied by editing the test, only by landing the bytes the drop shipped.
        /// </summary>
        [Test]
        public void EverySumInTheManifestMatchesTheBytesThatLanded()
        {
            string manifest = InKit("SHA256SUMS.txt");
            Assert.IsTrue(File.Exists(manifest), $"'{Kit}/SHA256SUMS.txt' is not in the checkout.");

            int checkedCount = 0;
            var wrong = new List<string>();
            foreach (string raw in File.ReadAllLines(manifest))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;

                int split = line.IndexOf(' ');
                Assert.Greater(split, 0, $"unparseable manifest line: '{line}'");
                string expected = line.Substring(0, split).Trim();
                string name = line.Substring(split).Trim();

                string abs = InKit(name);
                Assert.IsTrue(File.Exists(abs), $"SHA256SUMS names '{name}', which did not land.");

                string actual = Sha256Hex(NormaliseLf(File.ReadAllBytes(abs)));
                if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
                    wrong.Add($"{name}\n      manifest {expected}\n      landed   {actual}");
                checkedCount++;
            }

            Assert.AreEqual(18, checkedCount,
                "The kit manifest covers 18 files (2 sidecars, 8 rigs, 7 family manifests and the shore-plant " +
                "contract). A different count means the drop was re-cut — re-verify it before landing.");
            Assert.IsEmpty(wrong,
                "Landed bytes disagree with the kit's own manifest:\n   " + string.Join("\n   ", wrong) +
                "\n\nDo NOT re-stamp the manifest. A checksum rewritten to match what you have is not a checksum.");
        }

        /// <summary>
        /// The line-ending premise the manifest rests on. The kit was cut LF-only and hashed in that
        /// form, and it carries no <c>eol=lf</c> pin (this PR may not edit <c>.gitattributes</c>) — so a
        /// Windows checkout with <c>core.autocrlf=true</c> legitimately materialises the whole kit as
        /// CRLF. That is a checkout artefact, not a defect, and this guard does not forbid it.
        ///
        /// <para>What it forbids is what no checkout setting produces and no re-export should: a file
        /// that MIXES the two, or carries a lone CR. Either means the bytes were edited by hand
        /// somewhere between the art director and here — the one thing a drop exists to rule out. Gate 1
        /// cannot see this, because it hashes the LF-normalised form: a wholesale CRLF file verifies
        /// clean against the manifest, and a half-converted one is invisible to it.</para>
        /// </summary>
        [Test]
        public void NoKitTextFileMixesItsLineEndings()
        {
            string root = InKit("");
            string[] textExtensions = { ".js", ".json", ".md", ".txt" };

            var offenders = new List<string>();
            foreach (string path in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            {
                if (!textExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                    continue;

                byte[] bytes = File.ReadAllBytes(path);
                int lf = 0, crlf = 0, loneCr = 0;
                for (int i = 0; i < bytes.Length; i++)
                {
                    if (bytes[i] == 0x0A) lf++;
                    else if (bytes[i] == 0x0D)
                    {
                        if (i + 1 < bytes.Length && bytes[i + 1] == 0x0A) crlf++;
                        else loneCr++;
                    }
                }

                string name = path.Substring(root.Length).Replace(Path.DirectorySeparatorChar, FwdSlash);
                if (loneCr > 0) offenders.Add($"{name}: {loneCr} lone CR");
                else if (crlf != 0 && crlf != lf) offenders.Add($"{name}: {crlf} CRLF among {lf} lines");
            }

            Assert.IsEmpty(offenders,
                "These kit text files have had their line endings edited, not converted:" + Bullet +
                string.Join(Bullet, offenders) + Bullet +
                "A whole-file CRLF checkout is fine and expected on Windows. A file that is half one and " +
                "half the other is a file somebody opened and saved, and the drop stops being evidence of " +
                "what the art director sent.");
        }

        // =========================================================================================
        // Gate 2 — the sidecars name the rigs they were cut from
        // =========================================================================================

        /// <summary>
        /// Both gameplay sidecars stamp the rigs they were derived from. Every stamp is re-checked
        /// against the LANDED rig, so a sidecar cut from an older rig cannot ride in behind a newer
        /// one. This is the failure the kit's own manifest names outright: if a rig moves and the
        /// stamp does not, the sidecar is STALE — regenerate it, never re-stamp it.
        /// </summary>
        [Test]
        public void BothSidecarsStampTheRigsTheyWereActuallyCutFrom()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int stamps = 0;

            foreach (string sidecar in new[] { "TerrainPx.gameplay.json", "Greenery.gameplay.json" })
            {
                var derived = Dict(Json("Art/gameplay/" + sidecar), "derivedFrom");
                Assert.IsNotEmpty(derived, $"{sidecar} declares no derivedFrom — it cannot be traced to a rig.");

                foreach (var kv in derived)
                {
                    string rig = kv.Key;                  // already kit-relative, e.g. "Art/pxKit.js"
                    string expected = kv.Value as string;
                    Assert.IsNotNull(expected, $"{sidecar}: derivedFrom['{rig}'] is not a hash string.");

                    string abs = InKit(rig);
                    Assert.IsTrue(File.Exists(abs),
                        $"{sidecar} stamps '{rig}', which did not land. A sidecar whose rig is absent describes " +
                        "art nothing in this repo can regenerate.");

                    Assert.AreEqual(expected.ToLowerInvariant(),
                                    Sha256Hex(NormaliseLf(File.ReadAllBytes(abs))),
                                    $"{sidecar} was cut from a different '{rig}' than the one that landed.");

                    seen.Add(rig);
                    stamps++;
                }
            }

            Assert.AreEqual(11, stamps,
                "The two sidecars carry 11 derivedFrom stamps between them (3 on TerrainPx, 8 on Greenery).");
            CollectionAssert.AreEquivalent(
                new[]
                {
                    "Art/pxKit.js", "Art/pxKit2.js", "Art/pixelLanguage.js", "Art/flowerRig.js",
                    "Art/grassSpeciesRig.js", "Art/grassTuftRig.js", "Art/shorePlantRig.js",
                    "Art/driftWeedRig.js",
                },
                seen,
                "All eight kit rigs must be stamped by at least one sidecar — an unstamped rig is art with no " +
                "traceable source.");
        }

        // =========================================================================================
        // Gate 3 — the census, by count and then by NAME
        // =========================================================================================

        [Test]
        public void TheKitLandedFileForFile()
        {
            var wrong = new List<string>();
            foreach (var entry in Census)
            {
                string label = entry.Dir.Length == 0 ? "<kit root>" : entry.Dir;
                string abs = InKit(entry.Dir);
                if (!Directory.Exists(abs)) { wrong.Add($"{label}: MISSING"); continue; }

                int actual = Directory.GetFiles(abs).Length;
                if (actual != entry.Files) wrong.Add($"{label}: {actual}, expected {entry.Files}");
            }
            Assert.IsEmpty(wrong, "Per-directory census moved:\n   " + string.Join("\n   ", wrong));

            string[] all = Directory.GetFiles(InKit(""), "*", SearchOption.AllDirectories);
            Assert.AreEqual(TotalFiles, all.Length, $"The kit is {TotalFiles} files.");

            var byExtension = all
                .GroupBy(p => Path.GetExtension(p).ToLowerInvariant())
                .ToDictionary(g => g.Key, g => g.Count());

            int Count(string extension) => byExtension.TryGetValue(extension, out int n) ? n : 0;

            Assert.AreEqual(1237, Count(".png"),
                "1,237 PNGs — this is the number `git lfs ls-files` must report for the kit directory. " +
                "(The kit README's \"1,248 of them art\" counts the manifests too; it is not a PNG count.)");
            Assert.AreEqual(12, Count(".json"));
            Assert.AreEqual(9, Count(".js"));
            Assert.AreEqual(2, Count(".md"));
            Assert.AreEqual(1, Count(".txt"));
            Assert.AreEqual(5, byExtension.Count, "The kit has no file type beyond png/json/js/md/txt.");
        }

        /// <summary>
        /// The terrain vocabulary is a CROSS PRODUCT, and the whole point of a cross product is that
        /// a hole in it stays invisible until something reaches for the missing corner at runtime. So
        /// this generates all 355 names — 21 materials × 3 lighting steps × 5 channels, plus
        /// <c>_v2</c>/<c>_v3</c> on the four materials that carry variants — and demands the set on
        /// disk be exactly that, with no extras.
        /// </summary>
        [Test]
        public void TerrainPxShipsTheCompleteCrossProductAndNothingElse()
        {
            var channels = Dict(Json("Art/Textures/TerrainPx/TerrainPx.json"), "channels");
            CollectionAssert.AreEquivalent(
                new[] { "albedo", "_unlit", "_mask", "_normal", "_blend" }, channels.Keys,
                "The contract's channel list moved; the name generator below is built on it.");

            string[] materials = PngsIn("Art/Textures/TerrainPx")
                .Select(Stem)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToArray();

            Assert.AreEqual(21, materials.Length,
                "21 materials ship textures. The contract DECLARES 22 — sandstone's files were pulled on " +
                "purpose (the drop README's \"What is missing, on purpose\"); see the guard below.");

            var expected = new HashSet<string>(StringComparer.Ordinal);
            foreach (string m in materials)
            {
                foreach (string step in Steps)
                    foreach (string channel in TerrainChannels)
                        expected.Add(m + step + channel + ".png");

                if (!VariantMaterials.Contains(m, StringComparer.Ordinal)) continue;
                foreach (string variant in new[] { "_v2", "_v3" })
                    foreach (string channel in TerrainChannels)
                        expected.Add(m + variant + channel + ".png");
            }

            Assert.AreEqual(355, expected.Count, "17 plain materials × 15 + 4 variant materials × 25 = 355.");

            var got = new HashSet<string>(PngsIn("Art/Textures/TerrainPx"), StringComparer.Ordinal);

            var missing = expected.Except(got).OrderBy(s => s, StringComparer.Ordinal).ToArray();
            var extra = got.Except(expected).OrderBy(s => s, StringComparer.Ordinal).ToArray();
            Assert.IsEmpty(missing, "TerrainPx is missing:\n   " + string.Join("\n   ", missing));
            Assert.IsEmpty(extra,
                "TerrainPx carries textures the naming rule does not predict:\n   " + string.Join("\n   ", extra) +
                "\nEither the rule moved or a file was hand-dropped. Neither should be silent.");

            Assert.IsTrue(
                VariantMaterials.All(m => !got.Contains(m + "_Hi_v2.png") && !got.Contains(m + "_v2_Hi.png")),
                "Variants exist only at the key lighting step — a variant × step product would be 45 per material.");
        }

        /// <summary>
        /// <c>sandstone</c> is declared by the contract and ships nothing. That is deliberate (the
        /// drop README says so), and it is pinned here so the NEXT material to go quiet is not
        /// mistaken for the same intentional gap.
        /// </summary>
        [Test]
        public void TheOnlyDeclaredMaterialThatShipsNoTextureIsSandstone()
        {
            string[] declared = Dict(Json("Art/Textures/TerrainPx/TerrainPx.json"), "materials").Keys
                .OrderBy(s => s, StringComparer.Ordinal)
                .ToArray();
            Assert.AreEqual(22, declared.Length);

            var shipping = new HashSet<string>(
                PngsIn("Art/Textures/TerrainPx").Select(n => Stem(n).ToLowerInvariant()),
                StringComparer.Ordinal);

            CollectionAssert.AreEqual(
                new[] { "sandstone" },
                declared.Where(m => !shipping.Contains(m)).ToArray(),
                "Exactly one declared material ships no texture, and it is sandstone. A second silent material " +
                "means a bake dropped something.");

            var gameplay = Dict(Json("Art/gameplay/TerrainPx.gameplay.json"), "materials");
            Assert.AreEqual(21, gameplay.Count,
                "The gameplay sidecar describes only what ships — it must not carry traverse numbers for a " +
                "material with no art.");
            Assert.IsFalse(gameplay.ContainsKey("sandstone"));
        }

        /// <summary>
        /// Edge strips are generated from the manifest's OWN declarations: each strip ships exactly
        /// the orientations and corners it declares, in four channels. Wrack declares two orientations
        /// and no corners — it is a drift line, not a boundary between two grounds — so the manifest,
        /// not a constant, is what this counts against.
        /// </summary>
        [Test]
        public void EveryEdgeStripShipsExactlyTheOrientationsItDeclares()
        {
            var strips = Dict(Json("Art/Textures/TerrainPx/Edges/Edges.json"), "strips");
            Assert.AreEqual(17, strips.Count);

            var expected = new HashSet<string>(StringComparer.Ordinal);
            foreach (var kv in strips)
            {
                var block = kv.Value as Dictionary<string, object>;
                Assert.IsNotNull(block, $"strip '{kv.Key}' is not an object.");

                var directions = new List<string>();
                foreach (string list in new[] { "orientations", "corners" })
                {
                    var items = MiniJson.List(block, list);
                    if (items == null) continue;
                    directions.AddRange(items.Select(o => (string)o));
                }
                Assert.IsNotEmpty(directions, $"strip '{kv.Key}' declares no orientation at all.");

                foreach (string direction in directions)
                    foreach (string channel in EdgeChannels)
                        expected.Add($"{kv.Key}_{direction}{channel}.png");
            }

            Assert.AreEqual(776, expected.Count,
                "16 full strips × 12 directions × 4 channels + Wrack's 2 × 4 = 776.");

            var got = new HashSet<string>(PngsIn("Art/Textures/TerrainPx/Edges"), StringComparer.Ordinal);

            var missing = expected.Except(got).OrderBy(s => s, StringComparer.Ordinal).ToArray();
            var extra = got.Except(expected).OrderBy(s => s, StringComparer.Ordinal).ToArray();
            Assert.IsEmpty(missing, "Edges is missing:\n   " + string.Join("\n   ", missing));
            Assert.IsEmpty(extra, "Edges carries files its manifest does not declare:\n   " + string.Join("\n   ", extra));
        }

        // =========================================================================================
        // The drop's own contradictions — RECORDED, not corrected
        // =========================================================================================

        /// <summary>
        /// ⚠️ DROP ERRATUM #1. <c>Edges.json</c> lists five channels, the same five the tile contract
        /// uses — but no strip ships a <c>_blend</c>. Every edge PNG is one of albedo/_mask/_normal/
        /// _unlit, 194 apiece. The wiring PR must not assume an edge blend map exists; if the art
        /// director later bakes them this guard goes red, and the wiring can be told the truth. The
        /// landed manifest is NOT edited to match — a supplier's declaration is evidence.
        /// </summary>
        [Test]
        public void TheEdgesManifestDeclaresABlendChannelThatShipsOnNoStrip()
        {
            var channels = Dict(Json("Art/Textures/TerrainPx/Edges/Edges.json"), "channels");
            Assert.IsTrue(channels.ContainsKey("_blend"),
                "The erratum is gone — Edges.json no longer declares _blend. Re-read the drop and update the " +
                "PR record rather than deleting this guard on sight.");

            string[] pngs = PngsIn("Art/Textures/TerrainPx/Edges");

            Assert.IsEmpty(pngs.Where(n => n.EndsWith("_blend.png", StringComparison.Ordinal)).ToArray(),
                "An edge _blend map has appeared. The declaration is no longer an erratum — re-run the census " +
                "and tell the wiring lane it now has a blend channel on edges.");

            foreach (string suffix in new[] { "_mask.png", "_normal.png", "_unlit.png" })
                Assert.AreEqual(194, pngs.Count(n => n.EndsWith(suffix, StringComparison.Ordinal)),
                    $"every edge ships exactly one {suffix}");

            Assert.AreEqual(194,
                pngs.Count(n => !n.EndsWith("_mask.png", StringComparison.Ordinal) &&
                                !n.EndsWith("_normal.png", StringComparison.Ordinal) &&
                                !n.EndsWith("_unlit.png", StringComparison.Ordinal)),
                "albedo carries no suffix, so it is what is left over");
        }

        /// <summary>
        /// ⚠️ DROP ERRATUM #2. The kit README says the TerrainPx directory holds 358 files. It holds
        /// 357 — off by one, in the README's own census of its own directory. Recorded here so the
        /// number is never quietly reconciled in either direction. When the drop is re-cut this guard
        /// goes red: that is the signal to re-verify the census, not to re-word the README.
        /// </summary>
        [Test]
        public void TheKitReadmeMiscountsItsOwnTerrainPxDirectory()
        {
            string readme = File.ReadAllText(InKit("README.md"), Encoding.UTF8);
            Assert.IsTrue(readme.Contains("358"),
                "The README no longer claims 358 TerrainPx files — the drop was re-cut. Re-verify the census " +
                "and retire this erratum guard deliberately.");
            Assert.AreEqual(357, Directory.GetFiles(InKit("Art/Textures/TerrainPx")).Length,
                "355 PNGs + TerrainPx.json + README.md. The README's 358 is the erratum; the directory is the fact.");
        }

        // =========================================================================================
        // Gate 4 — the rigs actually run
        // =========================================================================================

        /// <summary>
        /// Every rig evaluates in the engine that bakes it, in the order the kit requires, and
        /// publishes the object the rest of the kit reaches for. Loading order is the guard that
        /// matters: <c>pxKit.js</c> throws by design without <c>pixelLanguage.js</c> ahead of it, so a
        /// chain assembled in the wrong order fails loudly here rather than baking a blank sheet.
        /// </summary>
        [Test]
        public void EveryRigEvaluatesInV8AndPublishesItsObject()
        {
            Assert.IsNull(_chainFailure, _chainFailure);

            foreach (var entry in Chain)
                Assert.IsTrue(
                    _host.EvaluateBool(
                        $"typeof globalThis.{entry.Global} === 'object' && globalThis.{entry.Global} !== null"),
                    $"{entry.File} evaluated but published no '{entry.Global}' — the kit's other rigs reach for " +
                    "it by that name.");

            Assert.Greater(_host.EvaluateNumber("Object.keys(PxLang).length"), 0);
            Assert.Greater(_host.EvaluateNumber("Object.keys(PxKit).length"), 0);
        }

        /// <summary>
        /// <b>The drop side of the shore-plant fork.</b> This copy of <c>shorePlantRig.js</c> accepts a
        /// tide by NAME and rejects a name it does not know. The repo's live copy does neither: it
        /// coerces an unknown string to NaN and draws a plant at a water level of "not a number",
        /// which is the kind of defect that survives review precisely because nothing throws.
        /// </summary>
        [Test]
        public void TheDropsShorePlantRigResolvesATideByNameAndRefusesAnUnknownOne()
        {
            Assert.IsNull(_chainFailure, _chainFailure);

            Assert.AreEqual("low,ebb,half,flood,high", _host.EvaluateString("ShorePlants.TIDE_KEYS.join(',')"));

            foreach (string key in new[] { "low", "ebb", "half", "flood", "high" })
            {
                Assert.IsTrue(_host.EvaluateBool($"isFinite(ShorePlants.tideOf('{key}').waterM)"),
                    $"tideOf('{key}') produced a non-finite water level — the string key was not resolved.");
                Assert.IsTrue(_host.EvaluateBool($"isFinite(ShorePlants.tideOf('{key}').t)"));
            }

            Assert.AreEqual(0.0, _host.EvaluateNumber("ShorePlants.tideOf('low').waterM"), 1e-9);
            Assert.AreEqual(2.2, _host.EvaluateNumber("ShorePlants.tideOf('half').waterM"), 1e-9);
            Assert.AreEqual(
                _host.EvaluateNumber("ShorePlants.tideOf('high').waterM"),
                _host.EvaluateNumber("ShorePlants.tideOf(1).waterM"), 1e-9,
                "a named tide and its numeric equivalent must land on the same water level");

            Assert.IsTrue(
                _host.EvaluateBool("(function(){ try { ShorePlants.tideOf('neap'); return false; } " +
                                   "catch (e) { return true; } })()"),
                "An unknown tide name must THROW. Silently coercing it to NaN is the bug this fork fixes.");
        }

        /// <summary>
        /// <b>The repo side of the same fork — and the reason this PR lands a COPY, not a replacement.</b>
        ///
        /// <para>The drop's <c>shorePlantRig.js</c> pre-dates ADR 0031, which turned the keyline off by
        /// default. The repo's copy carries that decision and lacks the tide fix; the drop's carries the
        /// tide fix and lacks the decision. They are a two-way fork, and the merge is the art director's
        /// to make, not ours. What this guard prevents is the cheap-looking fix: someone noticing "two
        /// copies of the same rig" and making them identical, which would silently switch the keyline
        /// back on across every shore plant in the game.</para>
        /// </summary>
        [Test]
        public void TheDropsShorePlantRigIsAForkAndMustNotReplaceTheLiveOne()
        {
            Assert.IsTrue(File.Exists(Full(LiveShorePlantRig)),
                $"'{LiveShorePlantRig}' is the rig this repo bakes from. It must still be there — this PR " +
                "replaces nothing.");

            Assert.AreNotEqual(LfSha(LiveShorePlantRig), LfSha(Kit + "/Art/shorePlantRig.js"),
                "The two shore-plant rigs are now byte-identical. One of them was overwritten with the other, " +
                "and whichever way round it happened a decision was lost: ADR 0031's keyline default, or the " +
                "tideOf string fix.");

            string live = File.ReadAllText(Full(LiveShorePlantRig), Encoding.UTF8);
            string kit = File.ReadAllText(InKit("Art/shorePlantRig.js"), Encoding.UTF8);

            StringAssert.Contains("KEYLINE_DEFAULT", live,
                "The live rig lost ADR 0031's keyline default. That decision is not the kit's to undo.");
            Assert.IsFalse(kit.Contains("KEYLINE_DEFAULT"),
                "The kit copy now carries ADR 0031 — it is no longer the pre-ADR fork this census describes.");

            StringAssert.Contains("unknown tide", kit, "the kit copy is the one carrying the tideOf fix");
            Assert.IsFalse(live.Contains("unknown tide"),
                "The live rig has gained the tide fix. The fork is closing — say so to the art director and " +
                "retire this guard deliberately, rather than letting it rot into a lie.");
        }

        /// <summary>
        /// No rig reaches for a clock or an RNG on any path that draws. The one <c>new Date()</c> in the
        /// kit is the <c>generated:</c> timestamp inside <c>shorePlantRig</c>'s contract emitter — a
        /// provenance stamp on a manifest, not a pixel — and the live repo copy carries it at the same
        /// place. Anything else would make a bake unreproducible.
        /// </summary>
        [Test]
        public void TheRigsCarryNoHiddenRandomnessAndNoClockThatDraws()
        {
            var offenders = new List<string>();
            foreach (var entry in Chain)
            {
                string[] lines = File.ReadAllLines(InKit(entry.File));
                for (int i = 0; i < lines.Length; i++)
                {
                    foreach (string forbidden in new[] { "Math.random", "Date.now", "performance.now" })
                        if (lines[i].Contains(forbidden))
                            offenders.Add($"{entry.File}:{i + 1} — {forbidden}");

                    if (lines[i].Contains("new Date") && !lines[i].Contains("generated:"))
                        offenders.Add($"{entry.File}:{i + 1} — new Date outside the contract stamp");
                }
            }

            Assert.IsEmpty(offenders,
                "A rig reached for a clock or an RNG:\n   " + string.Join("\n   ", offenders) +
                "\nEvery variation in these kits is seeded by the caller. A rig that rolls its own dice cannot " +
                "be re-baked to the same pixels, and the bake stops being evidence of anything.");
        }

        // =========================================================================================
        // Gate 5 — the sidecar's numbers and the contract's agree
        // =========================================================================================

        /// <summary>
        /// The gameplay sidecar claims its <c>blend</c> block travels verbatim from the tile contract.
        /// Claims like that are exactly what rots: the contract gets tuned, the sidecar does not, and
        /// the terrain blends one way while the footsteps think it blends another. So the two blocks
        /// are compared structurally, field for field.
        /// </summary>
        [Test]
        public void TheBlendBlockTravelsVerbatimFromTheContractToTheSidecar()
        {
            var contract = Json("Art/Textures/TerrainPx/TerrainPx.json") as Dictionary<string, object>;
            var sidecar = Json("Art/gameplay/TerrainPx.gameplay.json") as Dictionary<string, object>;
            Assert.IsNotNull(contract);
            Assert.IsNotNull(sidecar);

            Assert.IsTrue(contract.ContainsKey("blend"), "TerrainPx.json declares no blend block.");
            Assert.IsTrue(sidecar.ContainsKey("blend"), "TerrainPx.gameplay.json declares no blend block.");

            Assert.IsTrue(DeepEquals(contract["blend"], sidecar["blend"], "blend", out string where),
                $"The sidecar's blend block has parted company with the contract's at {where}. The sidecar says " +
                "it travels verbatim; make that true by regenerating it, not by editing one side.");
        }

        /// <summary>
        /// The traverse ladder, read off the sidecar and asserted as an ORDER rather than as eight magic
        /// numbers — so the art director can retune every value and this still means what it says: a
        /// made path is the fastest ground on the coast, and it gets slower, in this order, all the way
        /// down to an oyster reef.
        /// </summary>
        [Test]
        public void TheTraverseLadderRunsFromAMadePathDownToAnOysterReef()
        {
            var materials = Dict(Json("Art/gameplay/TerrainPx.gameplay.json"), "materials");
            string[] ladder = { "path", "grass", "foreshore", "sand", "shingle", "mud", "silt", "oysterreef" };

            var speeds = new List<(string Name, double Speed)>();
            foreach (string m in ladder)
            {
                Assert.IsTrue(materials.ContainsKey(m), $"the sidecar describes no '{m}'");
                speeds.Add((m, Num(MiniJson.Dict(materials[m], "traverse"), "speed_mul")));
            }

            string ladderText = string.Join(" > ", speeds.Select(s => $"{s.Name} {s.Speed}"));
            for (int i = 1; i < speeds.Count; i++)
                Assert.Less(speeds[i].Speed, speeds[i - 1].Speed,
                    $"{speeds[i].Name} ({speeds[i].Speed}) must be slower going than {speeds[i - 1].Name} " +
                    $"({speeds[i - 1].Speed}). Ladder as landed: {ladderText}");

            Assert.Greater(speeds[0].Speed, 1.0,
                "A made track is the only material faster than turf — that is what it is for.");
        }

        /// <summary>
        /// Drained rockweed on rock is the most treacherous footing in the kit, and the sidecar says so
        /// in its own note. Pinned as a MINIMUM over every material rather than as 0.18, so the claim
        /// survives retuning and only fails if something else becomes slipperier without anybody
        /// deciding it should.
        /// </summary>
        [Test]
        public void RockweedIsTheWorstFootingOnTheCoast()
        {
            var materials = Dict(Json("Art/gameplay/TerrainPx.gameplay.json"), "materials");

            var grips = materials
                .Select(kv => (Name: kv.Key, Grip: Num(MiniJson.Dict(kv.Value, "traverse"), "grip")))
                .OrderBy(g => g.Grip)
                .ToArray();

            Assert.AreEqual("rockweed", grips[0].Name,
                $"Lowest grip in the kit is rockweed, by design. Now it is {grips[0].Name} ({grips[0].Grip}). " +
                "Ladder: " + string.Join(", ", grips.Take(4).Select(g => $"{g.Name} {g.Grip}")));
            Assert.Less(grips[0].Grip, grips[1].Grip,
                "and it is the STRICT minimum — a tie means the hazard has stopped being distinctive");
        }

        // =========================================================================================
        // Gate 6 — the pixels are the size the manifests claim
        // =========================================================================================

        /// <summary>
        /// Every terrain tile is the size the contract declares, read from the PNG's own header. A tile
        /// that is not square at the declared grid tiles visibly, one seam per cell, across a region.
        /// </summary>
        [Test]
        public void EveryTerrainTileIsTheSizeTheContractDeclares()
        {
            var tile = MiniJson.List(Json("Art/Textures/TerrainPx/TerrainPx.json"), "tile");
            Assert.IsNotNull(tile, "TerrainPx.json declares no tile size.");
            int w = IntAt(tile, 0), h = IntAt(tile, 1);

            var wrong = new List<string>();
            foreach (string name in PngsIn("Art/Textures/TerrainPx"))
            {
                var size = PngSize(InKit("Art/Textures/TerrainPx/" + name));
                if (size.W != w || size.H != h) wrong.Add($"{name}: {size.W}×{size.H}");
            }
            Assert.IsEmpty(wrong, $"These tiles are not the declared {w}×{h}:\n   " + string.Join("\n   ", wrong));
        }

        /// <summary>
        /// Edge strips and corners, each against the size its own manifest declares for that direction —
        /// a N/S strip lies along the top, an E/W strip stands on its end, and a corner is a small
        /// square. Getting one transposed is a bug that looks like a texture bug and is really a naming
        /// bug.
        /// </summary>
        [Test]
        public void EveryEdgeStripIsTheSizeItsDirectionDeclares()
        {
            var sizes = Dict(Json("Art/Textures/TerrainPx/Edges/Edges.json"), "size");

            (int W, int H) Declared(string key)
            {
                var v = MiniJson.List(sizes, key);
                Assert.IsNotNull(v, $"Edges.json declares no size for '{key}'.");
                return (IntAt(v, 0), IntAt(v, 1));
            }

            var wrong = new List<string>();
            foreach (string name in PngsIn("Art/Textures/TerrainPx/Edges"))
            {
                // <Strip>_<dir>[_channel].png — the direction is the second underscore-separated field.
                string direction = name.Substring(0, name.Length - 4).Split('_')[1];
                var want = Declared(direction.Length == 1 ? direction : "corner");
                var got = PngSize(InKit("Art/Textures/TerrainPx/Edges/" + name));
                if (got.W != want.W || got.H != want.H)
                    wrong.Add($"{name}: {got.W}×{got.H}, declared {want.W}×{want.H}");
            }
            Assert.IsEmpty(wrong, "These edge textures are not their declared size:\n   " + string.Join("\n   ", wrong));
        }

        /// <summary>
        /// The two packed family sheets, against the <c>sheet</c> their manifests declare — and the
        /// cross-check that matters more: the declared grid of cells must actually fill the sheet. A
        /// sheet one row short slices into a final row of empty cells, and every consumer gets a species
        /// that renders as nothing.
        /// </summary>
        [Test]
        public void ThePackedSheetsAreTheSizeTheirManifestsDeclare()
        {
            var pages = new[]
            {
                ("Art/Foliage/Shrubs/Shrubs.json", "Art/Foliage/Shrubs/Shrubs.png"),
                ("Art/Foliage/Flowers/FlowersPx.json", "Art/Foliage/Flowers/FlowersPx.png"),
            };

            foreach (var page in pages)
            {
                object root = Json(page.Item1);
                var sheet = MiniJson.List(root, "sheet");
                var cell = MiniJson.List(root, "cell");
                Assert.IsNotNull(sheet, $"{page.Item1} declares no sheet size.");
                Assert.IsNotNull(cell, $"{page.Item1} declares no cell size.");

                int sw = IntAt(sheet, 0), sh = IntAt(sheet, 1);
                int cw = IntAt(cell, 0), ch = IntAt(cell, 1);

                var got = PngSize(InKit(page.Item2));
                Assert.AreEqual(sw, got.W, $"{page.Item2} is {got.W} wide; {page.Item1} declares {sw}.");
                Assert.AreEqual(sh, got.H, $"{page.Item2} is {got.H} tall; {page.Item1} declares {sh}.");

                Assert.AreEqual(0, sw % cw, $"{page.Item1}: a {sw}-wide sheet does not divide into {cw}-wide cells.");
                Assert.AreEqual(0, sh % ch, $"{page.Item1}: a {sh}-tall sheet does not divide into {ch}-tall cells.");

                var rows = MiniJson.List(root, "rows");
                Assert.IsNotNull(rows, $"{page.Item1} declares no rows.");
                Assert.AreEqual(rows.Count, sh / ch,
                    $"{page.Item1} names {rows.Count} rows but the sheet holds {sh / ch}. A row that is named and " +
                    "not drawn slices to empty cells, and the species renders as nothing.");
            }
        }

        /// <summary>
        /// The shrub sheet's three data channels are the same page size as its albedo. A mask a different
        /// size than the art it masks samples the wrong pixels everywhere, and looks like a shader bug
        /// for as long as it takes someone to check the files.
        /// </summary>
        [Test]
        public void TheShrubSheetsDataChannelsMatchItsAlbedo()
        {
            var albedo = PngSize(InKit("Art/Foliage/Shrubs/Shrubs.png"));
            foreach (string channel in new[] { "_mask", "_normal", "_unlit" })
            {
                var got = PngSize(InKit($"Art/Foliage/Shrubs/Shrubs{channel}.png"));
                Assert.AreEqual(albedo.W, got.W,
                    $"Shrubs{channel}.png is {got.W}×{got.H} against an albedo of {albedo.W}×{albedo.H}.");
                Assert.AreEqual(albedo.H, got.H,
                    $"Shrubs{channel}.png is {got.W}×{got.H} against an albedo of {albedo.W}×{albedo.H}.");
            }
        }
    }
}
