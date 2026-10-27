using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>EVERY META CARRIES ONE GUID UNITY CAN READ, AND NO OTHER META CARRIES IT.</b> On 2026-10-27 a
    /// hand-written <c>.meta</c> for a new C# interface carried a guid of <b>33</b> characters. Unity's YAML parser
    /// could not read it, logged "does not have a valid GUID and its corresponding Asset file will be ignored",
    /// compiled the Core assembly without that file, and the first user in another assembly failed with CS0246. A
    /// headless compile that reads the sources from disk had passed: it never opens a meta. Nothing in the repo
    /// guarded a meta's guid, so the editor was the first thing to notice, hours later and on a lent slot.
    ///
    /// <para>So this reads every tracked <c>.meta</c> under <see cref="MetaRoots"/> as TEXT and holds it to four
    /// rules, one test each so a report names the rule: <b>(a)</b> exactly one <c>guid:</c> line; <b>(b)</b> its
    /// value is exactly 32 characters, each 0-9 or a-f; <b>(c)</b> no two metas share a guid; <b>(d)</b> every file
    /// and folder under <c>Assets/</c> has its meta beside it, and every meta has its file or folder. It reads with
    /// <c>System.IO</c> only and never asks the AssetDatabase, which can only say what the importer already made of
    /// the files.</para>
    ///
    /// <para><b>Tracked</b> means what <c>git ls-files</c> lists from the project root, because a clone is what
    /// every other box opens. If git cannot run, the test walks <c>Assets/</c> and <c>Packages/</c> on disk instead,
    /// and every message says which of the two it read. <c>tools/check-metas.mjs</c> applies the same four rules
    /// with the same messages for lanes with no editor: change one, change the other.</para>
    ///
    /// <para><b>⚠ In an editor this reads the checkout AFTER Unity's import.</b> If that import has already
    /// rewritten a meta (Unity can answer two metas with one guid by giving one of them a new guid), this reads the
    /// rewritten file. The script reads the checkout before any editor has touched it: run it before you push.</para>
    /// </summary>
    public class AssetMetaGuidTests
    {
        // =====================================================================================
        // the rules, and what they read
        // =====================================================================================

        /// <summary>
        /// The roots whose metas are read: the project's assets and any embedded package. The first half of rule
        /// (d) covers <c>Assets/</c> only, since <c>Packages/manifest.json</c> rightly has no meta.
        /// </summary>
        public static readonly string[] MetaRoots = { "Assets", "Packages" };

        /// <summary>The four rules as a report names them, worded as <c>tools/check-metas.mjs</c> words them.</summary>
        public const string RuleA = "rule (a) one guid: line";
        public const string RuleB = "rule (b) 32 characters, each 0-9 or a-f";
        public const string RuleC = "rule (c) no shared guid";
        public const string RuleD = "rule (d) every file and folder has its meta, every meta its file or folder";

        /// <summary>How many offenders a failure message lists. The test's output carries them all.</summary>
        const int Shown = 40;

        /// <summary>How long <c>git ls-files</c> may take before the test walks the disk instead.</summary>
        const int GitTimeoutMs = 60_000;

        /// <summary>
        /// What the rules read: every path under <see cref="MetaRoots"/>, metas included, '/'-separated and in
        /// ordinal order; every folder those paths sit in; how to read a meta's text; and which source said so.
        /// </summary>
        public sealed class MetaTree
        {
            public List<string> Files;
            public HashSet<string> Folders;
            public Func<string, string> Read;
            public string Source;
        }

        /// <summary>One message per offender, per rule, in the order the script prints them.</summary>
        public sealed class MetaReport
        {
            public readonly List<string> A = new List<string>();
            public readonly List<string> B = new List<string>();
            public readonly List<string> C = new List<string>();
            public readonly List<string> D = new List<string>();
            public int Metas;
        }

        // =====================================================================================
        // the reading: git first, the disk if git cannot say
        // =====================================================================================

        /// <summary>
        /// The tracked tree at <paramref name="root"/>: what <c>git ls-files</c> lists there or, if git cannot run
        /// or lists nothing under <c>Assets/</c>, the <see cref="Walk"/> of the disk, with the reason.
        /// </summary>
        public static MetaTree Tracked(string root)
        {
            string why;
            try
            {
                var start = new ProcessStartInfo("git", "ls-files -z -- " + string.Join(" ", MetaRoots))
                {
                    WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8,
                };
                using (var git = Process.Start(start))
                {
                    var output = git.StandardOutput.ReadToEndAsync();
                    var error = git.StandardError.ReadToEndAsync();
                    if (!git.WaitForExit(GitTimeoutMs))
                    {
                        git.Kill();
                        why = $"git ls-files did not finish in {GitTimeoutMs / 1000} s";
                    }
                    else if (git.ExitCode != 0)
                    {
                        why = $"git could not run ({FirstLine(error.Result)})";
                    }
                    else
                    {
                        var files = output.Result.Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                        files.Sort(StringComparer.Ordinal);
                        if (files.Any(f => f.StartsWith("Assets/", StringComparison.Ordinal)))
                            return new MetaTree
                            {
                                Files = files, Folders = ImpliedFolders(files), Read = Reader(root),
                                Source = $"git ls-files, {files.Count} tracked paths",
                            };
                        why = "git ls-files listed nothing under Assets/";
                    }
                }
            }
            catch (Exception e)
            {
                why = $"git could not run ({FirstLine(e.Message)})";
            }
            return Walk(root, why);
        }

        /// <summary>
        /// Everything in <c>Assets/</c> and <c>Packages/</c> on disk, hidden and <c>~</c> names included: the rules
        /// decide what Unity ignores. Library, Temp, Logs, obj and build output sit beside them at the root, as
        /// <c>.gitignore</c> anchors them, so the walk never reads them. A folder named <c>Builds</c> inside
        /// <c>Assets/</c> (the character builds' Data folder) is content, and it is read.
        /// </summary>
        public static MetaTree Walk(string root, string why)
        {
            var files = new List<string>();
            var folders = new HashSet<string>(StringComparer.Ordinal);
            foreach (string top in MetaRoots)
                if (Directory.Exists(Path.Combine(root, top))) Visit(root, top, files, folders);
            files.Sort(StringComparer.Ordinal);
            return new MetaTree
            {
                Files = files, Folders = folders, Read = Reader(root),
                Source = $"walked Assets/ and Packages/ on disk because {why}; Library, Temp, Logs, obj and build " +
                         "output sit outside them and are not read",
            };
        }

        static void Visit(string root, string folder, List<string> files, HashSet<string> folders)
        {
            folders.Add(folder);
            string full = Path.Combine(root, folder);
            foreach (string file in Directory.GetFiles(full)) files.Add(folder + "/" + Path.GetFileName(file));
            foreach (string sub in Directory.GetDirectories(full))
                Visit(root, folder + "/" + Path.GetFileName(sub), files, folders);
        }

        /// <summary>Every folder above a tracked path. Git tracks no folders, so these are the folders a clone
        /// has.</summary>
        public static HashSet<string> ImpliedFolders(IEnumerable<string> files)
        {
            var folders = new HashSet<string>(StringComparer.Ordinal);
            foreach (string file in files)
                for (int slash = file.LastIndexOf('/'); slash > 0; slash = file.LastIndexOf('/', slash - 1))
                    if (!folders.Add(file.Substring(0, slash))) break;
            return folders;
        }

        static Func<string, string> Reader(string root) => path => File.ReadAllText(Path.Combine(root, path));

        static string FirstLine(string text) => (text ?? "").Trim().Split('\n')[0].Trim();

        // =====================================================================================
        // the rules (pure: the fixtures below prove them)
        // =====================================================================================

        static bool IsMeta(string path) => path.EndsWith(".meta", StringComparison.Ordinal);

        /// <summary>
        /// Unity imports nothing whose name starts with '.', ends with '~' or is <c>cvs</c>, and no file ending
        /// <c>.tmp</c>, so none of those has a meta: the <c>.gitkeep</c>s and the <c>Evidence~</c> folders here.
        /// The first part of <paramref name="path"/> is the root folder itself.
        /// </summary>
        public static bool UnityIgnores(string path, bool isFile)
        {
            string[] parts = path.Split('/');
            for (int i = 1; i < parts.Length; i++)
                if (parts[i].StartsWith(".", StringComparison.Ordinal) ||
                    parts[i].EndsWith("~", StringComparison.Ordinal) ||
                    parts[i].ToLowerInvariant() == "cvs")
                    return true;
            return isFile && parts[parts.Length - 1].ToLowerInvariant().EndsWith(".tmp", StringComparison.Ordinal);
        }

        /// <summary>Where a line ends, as YAML ends one: CRLF, CR or LF.</summary>
        static readonly string[] LineBreaks = { "\r\n", "\r", "\n" };

        /// <summary>
        /// The value of every line that starts <c>guid:</c>. An indented or inline <c>guid:</c> is a reference to
        /// another asset, not this meta's own.
        /// </summary>
        public static List<string> GuidValues(string text)
        {
            if (text.Length > 0 && text[0] == '\uFEFF') text = text.Substring(1);
            var values = new List<string>();
            foreach (string line in text.Split(LineBreaks, StringSplitOptions.None))
                if (line.StartsWith("guid:", StringComparison.Ordinal)) values.Add(line.Substring(5).Trim(' ', '\t'));
            return values;
        }

        /// <summary>Why <paramref name="guid"/> is not one Unity can read, or null if it is.</summary>
        public static string Malformed(string guid)
        {
            int bad = -1;
            for (int i = 0; i < guid.Length && bad < 0; i++)
                if (!((guid[i] >= '0' && guid[i] <= '9') || (guid[i] >= 'a' && guid[i] <= 'f'))) bad = i;
            if (guid.Length == 32 && bad < 0) return null;
            return $"guid '{guid}' has {guid.Length} characters" +
                   (bad < 0 ? "" : $", and character {bad + 1}, '{guid[bad]}', is not 0-9 or a-f") +
                   "; Unity needs exactly 32, each 0-9 or a-f";
        }

        /// <summary>
        /// All four rules over one tree. A meta that fails (a) is not read for (b), and only a well-formed guid is
        /// compared for (c), so one defect is named once, under the first rule it breaks.
        /// </summary>
        public static MetaReport Check(MetaTree tree)
        {
            var report = new MetaReport();
            var metas = tree.Files.Where(IsMeta).ToList();
            var metaSet = new HashSet<string>(metas, StringComparer.Ordinal);
            var assets = new HashSet<string>(tree.Files.Where(f => !IsMeta(f)), StringComparer.Ordinal);
            var byGuid = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
            report.Metas = metas.Count;

            foreach (string meta in metas)
            {
                List<string> values;
                try
                {
                    values = GuidValues(tree.Read(meta));
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    report.A.Add($"{meta}: could not be read ({FirstLine(e.Message)}), want exactly 1 guid: line");
                    continue;
                }
                if (values.Count != 1)
                {
                    report.A.Add($"{meta}: {values.Count} guid: lines, want exactly 1" +
                                 (values.Count > 0 ? ": " + string.Join(", ", values.Select(v => $"'{v}'")) : ""));
                    continue;
                }
                string bad = Malformed(values[0]);
                if (bad != null)
                {
                    report.B.Add($"{meta}: {bad}");
                    continue;
                }
                if (!byGuid.TryGetValue(values[0], out List<string> carriers))
                    byGuid[values[0]] = carriers = new List<string>();
                carriers.Add(meta);
            }
            foreach (var kv in byGuid)
                if (kv.Value.Count > 1)
                    report.C.Add($"guid {kv.Key} is in {kv.Value.Count} metas: {string.Join(", ", kv.Value)}");

            foreach (string file in tree.Files)
                if (!IsMeta(file) && file.StartsWith("Assets/", StringComparison.Ordinal) &&
                    !UnityIgnores(file, isFile: true) && !metaSet.Contains(file + ".meta"))
                    report.D.Add($"{file}: no meta beside it (want {file}.meta)");
            foreach (string folder in tree.Folders.OrderBy(f => f, StringComparer.Ordinal))
                if (folder.StartsWith("Assets/", StringComparison.Ordinal) && !UnityIgnores(folder, isFile: false) &&
                    !metaSet.Contains(folder + ".meta"))
                    report.D.Add($"{folder}/: no meta beside the folder (want {folder}.meta)");
            foreach (string meta in metas)
            {
                string asset = meta.Substring(0, meta.Length - ".meta".Length);
                if (!assets.Contains(asset) && !tree.Folders.Contains(asset))
                    report.D.Add($"{meta}: no file or folder beside it (want {asset})");
            }
            return report;
        }

        // =====================================================================================
        // the guard: one test per rule, on the tracked tree
        // =====================================================================================

        MetaTree _tree;
        MetaReport _real;

        /// <summary>The tracked tree's report, read once per run. Unity runs the tests from the project root, which
        /// is the repository root here.</summary>
        MetaReport Real()
        {
            if (_real == null)
            {
                _tree = Tracked(Directory.GetCurrentDirectory());
                _real = Check(_tree);
            }
            return _real;
        }

        void AssertRuleHolds(string rule, Func<MetaReport, List<string>> offendersOf, string fix)
        {
            MetaReport real = Real();
            Assert.Greater(real.Metas, 0,
                $"{rule}: not one .meta under Assets/ or Packages/ ({_tree.Source}). This guard would then be " +
                "guarding nothing; treat that as a failure, not a pass.");

            List<string> offenders = offendersOf(real);
            TestContext.WriteLine($"[AssetMetaGuid] {rule}: {offenders.Count} offender(s) in {real.Metas} metas " +
                                  $"({_tree.Source})" +
                                  (offenders.Count == 0 ? "." : ":\n  " + string.Join("\n  ", offenders)));

            var message = new StringBuilder();
            message.AppendLine($"{rule}: {offenders.Count} offender(s) in {real.Metas} metas ({_tree.Source}):");
            foreach (string offender in offenders.Take(Shown)) message.AppendLine("  " + offender);
            if (offenders.Count > Shown)
                message.AppendLine($"  … and {offenders.Count - Shown} more; the test's output lists them all.");
            message.Append(fix);
            Assert.IsEmpty(offenders, message.ToString());
        }

        [Test]
        public void EveryMeta_HasExactlyOneGuidLine()
        {
            AssertRuleHolds(RuleA, report => report.A,
                "A meta with no guid: line gives Unity no id for its asset; two are usually a merge that kept both " +
                "sides. Keep the guid the asset was committed with, the one other files already reference, and " +
                "never mint a new one to make this green.");
        }

        [Test]
        public void EveryMetaGuid_Is32Characters_Each0To9OrAToF()
        {
            AssertRuleHolds(RuleB, report => report.B,
                "This is how 2026-10-27 broke: Unity cannot read such a guid, ignores the asset, and compiles its " +
                "assembly without it, so the first user elsewhere fails with CS0246. Put back the 32 characters the " +
                "meta was committed with (git log -p on the meta has them); a brand-new meta can take " +
                "node -e \"console.log(crypto.randomUUID().replaceAll('-', ''))\".");
        }

        [Test]
        public void NoTwoMetas_ShareAGuid()
        {
            AssertRuleHolds(RuleC, report => report.C,
                "Two metas with one guid are usually a file copied with its meta. Unity keeps the guid for one and " +
                "gives the other a new one, so a reference can land on the wrong asset or on none. Give the COPY a " +
                "new guid; the original keeps the one its references already name.");
        }

        [Test]
        public void EveryFileAndFolderUnderAssets_HasItsMeta_AndEveryMetaItsFileOrFolder()
        {
            AssertRuleHolds(RuleD, report => report.D,
                "An asset committed without its meta gets a new guid in every clone, so nothing that references it " +
                "resolves; a meta without its asset is a leftover from a move or a delete. Commit each meta beside " +
                "its file or folder, or delete the leftover. What Unity never imports (a name starting '.', ending " +
                "'~', or cvs) needs none.");
        }

        // =====================================================================================
        // the fixtures: each tripwire is connected, and fires alone
        // =====================================================================================

        // The tree and the defects tools/check-metas.mjs was run against for this guard's PR, so the two can be seen
        // to name every defect in the same words.
        const string FolderGuid = "a1b2c3d4e5f60718293a4b5c6d7e8f90";
        const string NoteGuid = "0f1e2d3c4b5a69788796a5b4c3d2e1f0";
        const string SubGuid = "11112222333344445555666677778888";
        const string DeepGuid = "9999aaaabbbbccccddddeeeeffff0000";
        const string OarGuid = "abcdef0123456789abcdef0123456789";
        const string OtherGuid = "3c4d5e6f708192a3b4c5d6e7f8091a2b";

        static string Meta(string guid) =>
            "fileFormatVersion: 2\nguid: " + guid + "\nDefaultImporter:\n  externalObjects: {}\n  userData: \n" +
            "  assetBundleName: \n  assetBundleVariant: \n";

        static string FolderMeta(string guid) =>
            "fileFormatVersion: 2\nguid: " + guid + "\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n" +
            "  userData: \n  assetBundleName: \n  assetBundleVariant: \n";

        /// <summary>A meta that also names ANOTHER asset's guid, inline and indented, as an importer's remap
        /// does: that is a reference, not a second guid line, and not a shared guid.</summary>
        static string NoteMeta(string guid) =>
            "fileFormatVersion: 2\nguid: " + guid + "\nTextScriptImporter:\n  externalObjects:\n  - first:\n" +
            "      type: UnityEngine:TextAsset\n      assembly: UnityEngine.CoreModule\n      name: Note\n" +
            "    second: {fileID: 4900000, guid: " + DeepGuid + ", type: 3}\n  userData: \n" +
            "  assetBundleName: \n  assetBundleVariant: \n";

        /// <summary>
        /// A clean tree in miniature: a folder and a subfolder with their metas, files in each, and what Unity never
        /// imports beside them (a <c>.gitkeep</c>, a <c>~</c> folder) with no meta, as the real tree has them. One
        /// meta is CRLF, and one is hand-written in two lines ending in a lone CR, the shape
        /// <c>DoryOarMathTests.cs.meta</c> has carried since July: YAML ends a line at either, so both guids read as
        /// 32 characters.
        /// </summary>
        static SortedDictionary<string, string> CleanTree() =>
            new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["Assets/Fixture.meta"] = FolderMeta(FolderGuid),
                ["Assets/Fixture/.gitkeep"] = "",
                ["Assets/Fixture/Evidence~/shot.png"] = "",
                ["Assets/Fixture/Note.txt"] = "a note\n",
                ["Assets/Fixture/Note.txt.meta"] = NoteMeta(NoteGuid),
                ["Assets/Fixture/Sub.meta"] = FolderMeta(SubGuid),
                ["Assets/Fixture/Sub/Deep.txt"] = "deep\n",
                ["Assets/Fixture/Sub/Deep.txt.meta"] = Meta(DeepGuid).Replace("\n", "\r\n"),
                ["Assets/Fixture/Sub/Oar.txt"] = "oar\n",
                ["Assets/Fixture/Sub/Oar.txt.meta"] = "fileFormatVersion: 2\nguid: " + OarGuid + "\r",
                ["Packages/manifest.json"] = "{ \"dependencies\": {} }\n",
            };

        /// <summary>The rules over a tree held in memory, its folders implied the way git implies them.</summary>
        static MetaReport CheckFixture(IDictionary<string, string> files)
        {
            var paths = files.Keys.ToList();
            paths.Sort(StringComparer.Ordinal);
            return Check(new MetaTree
            {
                Files = paths, Folders = ImpliedFolders(paths), Read = path => files[path], Source = "(fixture)",
            });
        }

        /// <summary>
        /// Exactly <paramref name="expected"/> under <paramref name="rule"/>, and nothing under the other three. A
        /// defect named twice is a report nobody can act on; a defect named nowhere is the hole this guard closes.
        /// </summary>
        static void AssertOnly(MetaReport report, string rule, params string[] expected)
        {
            var rules = new[] { (RuleA, report.A), (RuleB, report.B), (RuleC, report.C), (RuleD, report.D) };
            foreach (var (name, found) in rules)
                CollectionAssert.AreEqual(name == rule ? expected : new string[0], found,
                    $"{name} named {found.Count} offender(s) where the fixture expects " +
                    $"{(name == rule ? expected.Length : 0)}:\n  " + string.Join("\n  ", found));
        }

        [Test]
        public void TheRules_PassACleanTree_AndAskNoMetaOfWhatUnityIgnores()
        {
            MetaReport report = CheckFixture(CleanTree());
            Assert.AreEqual(5, report.Metas, "the fixture's five metas were not all read");
            AssertOnly(report, rule: null);
        }

        [Test]
        public void RuleA_NamesAMetaWithTwoGuidLines_AndNothingElse()
        {
            // A merge that kept both sides' guid lines.
            var tree = CleanTree();
            tree["Assets/Fixture/Note.txt.meta"] = NoteMeta(NoteGuid + "\nguid: " + OtherGuid);
            AssertOnly(CheckFixture(tree), RuleA,
                "Assets/Fixture/Note.txt.meta: 2 guid: lines, want exactly 1: " +
                "'0f1e2d3c4b5a69788796a5b4c3d2e1f0', '3c4d5e6f708192a3b4c5d6e7f8091a2b'");
        }

        [Test]
        public void RuleB_NamesA33CharacterGuid_AndNothingElse()
        {
            // 2026-10-27's meta: one character too many.
            var tree = CleanTree();
            tree["Assets/Fixture/Note.txt.meta"] = NoteMeta(NoteGuid + "0");
            AssertOnly(CheckFixture(tree), RuleB,
                "Assets/Fixture/Note.txt.meta: guid '0f1e2d3c4b5a69788796a5b4c3d2e1f00' has 33 characters; " +
                "Unity needs exactly 32, each 0-9 or a-f");
        }

        [Test]
        public void RuleB_NamesAnUppercaseGuid_AndNothingElse()
        {
            var tree = CleanTree();
            tree["Assets/Fixture/Note.txt.meta"] = NoteMeta("0F1E2D3C4B5A69788796A5B4C3D2E1F0");
            AssertOnly(CheckFixture(tree), RuleB,
                "Assets/Fixture/Note.txt.meta: guid '0F1E2D3C4B5A69788796A5B4C3D2E1F0' has 32 characters, and " +
                "character 2, 'F', is not 0-9 or a-f; Unity needs exactly 32, each 0-9 or a-f");
        }

        [Test]
        public void RuleC_NamesTwoMetasSharingAGuid_AndNothingElse()
        {
            // A file copied with its meta.
            var tree = CleanTree();
            tree["Assets/Fixture/Copy.txt"] = "a note\n";
            tree["Assets/Fixture/Copy.txt.meta"] = NoteMeta(NoteGuid);
            AssertOnly(CheckFixture(tree), RuleC,
                "guid 0f1e2d3c4b5a69788796a5b4c3d2e1f0 is in 2 metas: Assets/Fixture/Copy.txt.meta, " +
                "Assets/Fixture/Note.txt.meta");
        }

        [Test]
        public void RuleD_NamesAFileWithoutItsMeta_AndNothingElse()
        {
            var tree = CleanTree();
            tree["Assets/Fixture/Bare.txt"] = "no meta\n";
            AssertOnly(CheckFixture(tree), RuleD,
                "Assets/Fixture/Bare.txt: no meta beside it (want Assets/Fixture/Bare.txt.meta)");
        }

        [Test]
        public void RuleD_NamesAFolderWithoutItsMeta_AndNothingElse()
        {
            var tree = CleanTree();
            tree["Assets/Fixture/New/Item.txt"] = "item\n";
            tree["Assets/Fixture/New/Item.txt.meta"] = Meta(OtherGuid);
            AssertOnly(CheckFixture(tree), RuleD,
                "Assets/Fixture/New/: no meta beside the folder (want Assets/Fixture/New.meta)");
        }

        [Test]
        public void RuleD_NamesAMetaWithoutItsFile_AndNothingElse()
        {
            var tree = CleanTree();
            tree["Assets/Fixture/Gone.txt.meta"] = Meta(OtherGuid);
            AssertOnly(CheckFixture(tree), RuleD,
                "Assets/Fixture/Gone.txt.meta: no file or folder beside it (want Assets/Fixture/Gone.txt)");
        }

        [Test]
        public void TheWalk_ReadsWhatGitWouldList_AndNothingOutsideAssetsAndPackages()
        {
            // The fallback, on disk: the clean tree, plus Unity's own Library and Temp at the root, each holding a
            // meta that breaks every rule. The walk must list exactly what git would and never read those two.
            var tree = CleanTree();
            string root = Path.Combine(Path.GetTempPath(), "hh_meta_guid_fixture_" + Guid.NewGuid().ToString("N"));
            try
            {
                foreach (var kv in tree) Write(root, kv.Key, kv.Value);
                Write(root, "Library/PackageCache/com.example.kit/Kit.cs.meta", "guid: not-a-guid\nguid: \n");
                Write(root, "Temp/Orphan.txt.meta", "");

                MetaTree walked = Walk(root, "the fixture asked for it");
                StringAssert.StartsWith("walked Assets/ and Packages/ on disk because the fixture asked for it",
                                        walked.Source);
                CollectionAssert.AreEqual(tree.Keys, walked.Files,
                    "the walk did not list exactly the fixture's files:\n  " + string.Join("\n  ", walked.Files));
                CollectionAssert.AreEquivalent(ImpliedFolders(tree.Keys), walked.Folders,
                    "the walk did not find exactly the folders git would imply");

                MetaReport report = Check(walked);
                Assert.AreEqual(5, report.Metas, "the walk did not read the fixture's five metas");
                AssertOnly(report, rule: null);
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
            }
        }

        static void Write(string root, string path, string text)
        {
            string full = Path.Combine(root, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, text);
        }
    }
}
