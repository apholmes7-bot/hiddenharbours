using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
#if UNITY_EDITOR
using HiddenHarbours.Art.Editor;
#else
using System.Web.Script.Serialization;
#endif

namespace HiddenHarbours.Tests.EditMode
{
    // Reads source files only. It does not predict imported texture sizes or build compression.
    public static class DemoContentClosure
    {
        [Serializable]
        public sealed class Budget
        {
            public string[] sceneRoots;
            public string[] settingsRoots;
            public string[] foreignFolderPrefixes;
            public long recordedBytes;
            public long growthMarginBytes;
            public KnownException[] knownExceptions;
            public ImporterInputs[] importerInputs;
            public SourceContract[] sourceContracts;
        }

        [Serializable]
        public sealed class ImporterInputs
        {
            public string asset;
            public string importer;
            public string[] sources;
        }

        [Serializable]
        public sealed class SourceContract
        {
            public string path;
            public string sha256;
        }

        [Serializable]
        public sealed class KnownException
        {
            public string rule;
            public string source;
            public string objectId;
            public string target;
            public string reason;
        }

        public sealed class Reference
        {
            public string Source;
            public string ObjectId;
            public string Guid;
            public string Target;
            public int Line;
            public string Key { get { return Source + "|" + ObjectId + "|" + Target; } }
            public override string ToString()
            {
                return Source + ":" + Line + " object &" + ObjectId + " -> " + (Target ?? Guid);
            }
        }

        public sealed class Asset
        {
            public string Path;
            public long Bytes;
            public bool Pointer;
            public bool Opaque;
            public readonly List<Reference> References = new List<Reference>();
        }

        public sealed class Report
        {
            public readonly SortedDictionary<string, Asset> Assets = new SortedDictionary<string, Asset>(StringComparer.Ordinal);
            public readonly SortedDictionary<string, string> Roots = new SortedDictionary<string, string>(StringComparer.Ordinal);
            public readonly SortedDictionary<string, string> Parent = new SortedDictionary<string, string>(StringComparer.Ordinal);
            public readonly List<Reference> Unresolved = new List<Reference>();
            public readonly List<Reference> ForeignSceneReferences = new List<Reference>();
            public readonly List<string> Problems = new List<string>();
            public long ReachableBytes { get { return Parent.Keys.Sum(p => Assets[p].Bytes); } }
            public long UnreachableBytes { get { return Assets.Where(p => !Parent.ContainsKey(p.Key)).Sum(p => p.Value.Bytes); } }

            public string Chain(string path)
            {
                var chain = new List<string>();
                while (path != null)
                {
                    chain.Add(path);
                    path = Parent[path];
                }
                chain.Reverse();
                return string.Join(" -> ", chain.ToArray());
            }
        }

        static readonly Regex GuidReference = new Regex(@"\bguid:\s*([0-9a-fA-F]{32})\b");
        static readonly Regex JsonGuidReference = new Regex("\\\"(?:guid|m_AssetGUID)\\\"\\s*:\\s*\\\"([0-9a-fA-F]{32})\\\"");
        static readonly Regex ObjectHeader = new Regex(@"^--- !u!\d+ &(-?\d+)");
        static readonly Regex Include = new Regex("^\\s*#include(?:_with_pragmas)?\\s*[\\\"<]([^\\\">]+)[\\\">]");
        static readonly HashSet<string> SourceExtensions = new HashSet<string>(new[]
            { ".cs", ".dll", ".asmdef", ".asmref", ".rsp", ".hlsl", ".cginc", ".compute", ".shader" }, StringComparer.OrdinalIgnoreCase);
        static readonly HashSet<string> BinaryExtensions = new HashSet<string>(new[]
            { ".png", ".jpg", ".jpeg", ".tga", ".psd", ".psb", ".aseprite", ".gif", ".exr", ".hdr", ".dds",
              ".wav", ".ogg", ".mp3", ".aif", ".aiff", ".flac", ".ttf", ".otf", ".woff", ".dll", ".pdb",
              ".fbx", ".blend", ".mp4", ".webm", ".unitypackage", ".zip" }, StringComparer.OrdinalIgnoreCase);

        public static bool IsPlayerSource(string path)
        {
            string[] parts = path.Replace('\\', '/').Split('/');
            if (parts.Any(p => p == "Editor" || p == "Editor Default Resources" || p == "Gizmos" || p == "Tests" ||
                p.StartsWith(".", StringComparison.Ordinal) || p.EndsWith("~", StringComparison.Ordinal) ||
                p.Equals("cvs", StringComparison.OrdinalIgnoreCase))) return false;
            return !path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) &&
                   !path.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsForeignFolder(string path, IEnumerable<string> prefixes)
        {
            string[] folders = path.Split('/');
            return folders.Take(folders.Length - 1).Any(f => prefixes.Any(p => f.StartsWith(p, StringComparison.OrdinalIgnoreCase)));
        }

        public static bool TryPointerSize(string text, out long bytes)
        {
            bytes = 0;
            if (!text.StartsWith("version https://git-lfs.github.com/spec/v1\n", StringComparison.Ordinal) &&
                !text.StartsWith("version https://git-lfs.github.com/spec/v1\r\n", StringComparison.Ordinal)) return false;
            Match size = Regex.Match(text, @"(?m)^size ([0-9]+)\r?$");
            if (!Regex.IsMatch(text, @"(?m)^oid sha256:[0-9a-f]{64}\r?$") || !size.Success ||
                !long.TryParse(size.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out bytes))
                throw new InvalidDataException("DemoContent.InvalidLfsPointer: malformed size or object id");
            return true;
        }

        public static List<Reference> ReadReferences(string source, IEnumerable<string> lines, IDictionary<string, string> index)
        {
            var refs = new List<Reference>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            string objectId = "0";
            int lineNumber = 0;
            bool buildSettings = source == "ProjectSettings/EditorBuildSettings.asset";
            bool configObjects = !buildSettings;
            foreach (string line in lines)
            {
                lineNumber++;
                Match header = ObjectHeader.Match(line);
                if (header.Success) objectId = header.Groups[1].Value;
                if (buildSettings && line.StartsWith("  m_configObjects:", StringComparison.Ordinal)) configObjects = true;
                if (!configObjects || (source.EndsWith(".meta", StringComparison.Ordinal) && line.StartsWith("guid:", StringComparison.Ordinal))) continue;
                foreach (Match match in GuidReference.Matches(line).Cast<Match>().Concat(JsonGuidReference.Matches(line).Cast<Match>()))
                {
                    string guid = match.Groups[1].Value.ToLowerInvariant();
                    if (guid.StartsWith("0000000000000000", StringComparison.Ordinal) || !seen.Add(objectId + "|" + guid)) continue;
                    string target;
                    index.TryGetValue(guid, out target);
                    refs.Add(new Reference { Source = source, ObjectId = objectId, Guid = guid, Target = target, Line = lineNumber });
                }
            }
            return refs;
        }

        public static Report Build(string root, Budget budget, IDictionary<string, string> index)
        {
            root = Path.GetFullPath(root).TrimEnd('/', '\\');
            var report = new Report();
            foreach (SourceContract contract in budget.sourceContracts ?? new SourceContract[0])
                if (!File.Exists(Path.Combine(root, contract.path)) || TextHash(Path.Combine(root, contract.path)) != contract.sha256)
                    report.Problems.Add("DemoContent.ImportContractChanged: re-audit string dependencies in " + contract.path);
            foreach (string folder in new[] { "Assets", "Packages" })
            {
                string absolute = Path.Combine(root, folder);
                if (!Directory.Exists(absolute)) continue;
                foreach (string file in Directory.GetFiles(absolute, "*", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
                {
                    string path = file.Substring(root.TrimEnd('/', '\\').Length + 1).Replace('\\', '/');
                    if (!IsPlayerSource(path) || (folder == "Packages" && !File.Exists(file + ".meta"))) continue;
                    Asset asset = ReadAsset(root, path, index);
                    report.Assets.Add(path, asset);
                    string reason = RootReason(path);
                    if (reason != null) report.Roots.Add(path, reason);
                }
            }
            foreach (string scene in budget.sceneRoots)
            {
                if (!report.Assets.ContainsKey(scene)) throw new FileNotFoundException("DemoContent.MissingScene: " + scene);
                report.Roots[scene] = "scene";
                report.ForeignSceneReferences.AddRange(report.Assets[scene].References.Where(r => r.Target != null &&
                    IsForeignFolder(r.Target, budget.foreignFolderPrefixes)));
            }
            foreach (string settings in budget.settingsRoots)
            {
                if (!File.Exists(Path.Combine(root, settings))) throw new FileNotFoundException("DemoContent.MissingSettings: " + settings);
                foreach (Reference reference in ReadReferences(settings, File.ReadLines(Path.Combine(root, settings)), index))
                {
                    if (reference.Target != null && report.Assets.ContainsKey(reference.Target)) report.Roots[reference.Target] = settings;
                    else report.Unresolved.Add(reference);
                }
            }
            foreach (ImporterInputs inputs in budget.importerInputs ?? new ImporterInputs[0])
            {
                Asset recipe;
                if (!report.Assets.TryGetValue(inputs.asset, out recipe))
                    throw new FileNotFoundException("DemoContent.MissingImportRecipe: " + inputs.asset);
                recipe.References.RemoveAll(r => r.Source == inputs.asset + ".meta" && r.Target == inputs.importer);
                foreach (string source in inputs.sources)
                    recipe.References.Add(new Reference { Source = inputs.asset, ObjectId = "import", Target = source });
            }
            Traverse(report);
            foreach (Asset asset in report.Assets.Values.Where(a => a.Pointer && !BinaryExtensions.Contains(Path.GetExtension(a.Path))))
                if (report.Parent.ContainsKey(asset.Path)) report.Problems.Add("DemoContent.UnreadableDependencies: " + asset.Path);
            foreach (Asset asset in report.Assets.Values.Where(a => a.Opaque && !BinaryExtensions.Contains(Path.GetExtension(a.Path))))
                if (report.Parent.ContainsKey(asset.Path)) report.Problems.Add("DemoContent.UnreadableDependencies: " + asset.Path);
            return report;
        }

        public static string TextHash(string path)
        {
            string text = File.ReadAllText(path).Replace("\r\n", "\n");
            if (path.Replace('\\', '/').EndsWith("/Packages/manifest.json", StringComparison.Ordinal))
                return ManifestHash(text);
            return Hash(text);
        }

        // Object order and whitespace are not package changes; every key, value and array order is.
        public static string ManifestHash(string json)
        {
#if UNITY_EDITOR
            return Hash(CanonicalValue(MiniJson.Parse(json)));
#else
            return Hash(CanonicalValue(new JavaScriptSerializer().DeserializeObject(json)));
#endif
        }

        static string CanonicalValue(object value)
        {
            if (value == null) return "null;";
            var map = value as Dictionary<string, object>;
            if (map != null)
                return "{" + string.Concat(map.OrderBy(p => p.Key, StringComparer.Ordinal)
                    .Select(p => CanonicalValue(p.Key) + CanonicalValue(p.Value))) + "}";
            var list = value as IEnumerable<object>;
            if (list != null) return "[" + string.Concat(list.Select(CanonicalValue)) + "]";
            var text = value as string;
            if (text != null) return "s" + text.Length.ToString(CultureInfo.InvariantCulture) + ":" + text;
            if (value is bool) return (bool)value ? "true;" : "false;";
            if (value is double) return "n" + ((double)value).ToString("R", CultureInfo.InvariantCulture) + ";";
            if (value is int || value is long || value is decimal)
                return "n" + Convert.ToString(value, CultureInfo.InvariantCulture) + ";";
            throw new InvalidDataException("DemoContent.InvalidManifestValue");
        }

        static string Hash(string text)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text)))
                    .Replace("-", "").ToLowerInvariant();
        }

        static string RootReason(string path)
        {
            if (path.Split('/').Contains("Resources")) return "Resources";
            if (path.StartsWith("Assets/StreamingAssets/", StringComparison.Ordinal)) return "StreamingAssets";
            if (SourceExtensions.Contains(Path.GetExtension(path))) return "runtime source / shader / plugin (conservative)";
            return null;
        }

        static Asset ReadAsset(string root, string path, IDictionary<string, string> index)
        {
            string absolute = Path.Combine(root, path);
            var asset = new Asset { Path = path, Bytes = new FileInfo(absolute).Length };
            byte[] head = new byte[512];
            int count;
            using (var stream = File.OpenRead(absolute)) count = stream.Read(head, 0, head.Length);
            long size;
            asset.Pointer = TryPointerSize(Encoding.UTF8.GetString(head, 0, count), out size);
            if (asset.Pointer) asset.Bytes = size;
            asset.Opaque = !asset.Pointer && head.Take(count).Contains((byte)0);
            string extension = Path.GetExtension(path);
            if (!asset.Pointer && !asset.Opaque && !BinaryExtensions.Contains(extension))
            {
                // Code is retained conservatively, but GUIDs in comments are not serialized links.
                if (!SourceExtensions.Contains(extension)) asset.References.AddRange(ReadReferences(path, File.ReadLines(absolute), index));
                if (extension == ".shader" || extension == ".hlsl" || extension == ".cginc" || extension == ".compute")
                {
                    foreach (string line in File.ReadLines(absolute))
                    {
                        Match include = Include.Match(line);
                        if (!include.Success) continue;
                        string target = include.Groups[1].Value;
                        // Package include paths are explicit external dependencies even when the
                        // editor can resolve them through its package cache. Their bytes are excluded.
                        if (target.StartsWith("Packages/", StringComparison.Ordinal))
                        {
                            asset.References.Add(new Reference { Source = path, ObjectId = "include", Target = target });
                            continue;
                        }
                        bool projectInclude = target.StartsWith("Assets/", StringComparison.Ordinal);
                        if (!target.StartsWith("Assets/", StringComparison.Ordinal) && !target.StartsWith("Packages/", StringComparison.Ordinal))
                            target = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(absolute), target));
                        else target = Path.Combine(root, target);
                        if ((projectInclude || File.Exists(target)) && target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                            asset.References.Add(new Reference { Source = path, ObjectId = "include", Target = target.Substring(root.Length + 1).Replace('\\', '/') });
                    }
                }
            }
            if (File.Exists(absolute + ".meta")) asset.References.AddRange(ReadReferences(path + ".meta", File.ReadLines(absolute + ".meta"), index));
            return asset;
        }

        public static void Traverse(Report report)
        {
            var queue = new Queue<string>();
            foreach (string root in report.Roots.Keys)
            {
                if (!report.Assets.ContainsKey(root)) throw new InvalidDataException("DemoContent.MissingRoot: " + root);
                report.Parent[root] = null;
                queue.Enqueue(root);
            }
            while (queue.Count != 0)
            {
                string source = queue.Dequeue();
                foreach (Reference reference in report.Assets[source].References)
                {
                    if (reference.Target == null || !report.Assets.ContainsKey(reference.Target))
                    {
                        report.Unresolved.Add(reference);
                        continue;
                    }
                    if (report.Parent.ContainsKey(reference.Target)) continue;
                    report.Parent[reference.Target] = source;
                    queue.Enqueue(reference.Target);
                }
            }
        }

        public static List<string> Violations(Report report, Budget budget)
        {
            var problems = new List<string>(report.Problems);
            if (budget.recordedBytes < 0 || budget.growthMarginBytes < 0)
                throw new ArgumentException("DemoContent.InvalidBudget: byte values must be nonnegative");
            long ceiling = checked(budget.recordedBytes + budget.growthMarginBytes);
            if (report.ReachableBytes > ceiling)
                problems.Add("DemoContent.ReachableSizeGrew: " + report.ReachableBytes + " bytes exceeds " + ceiling +
                    " (recorded " + budget.recordedBytes + ", margin " + budget.growthMarginBytes + "). Inspect the dependency chains before changing the budget.");
            var observed = new HashSet<string>(StringComparer.Ordinal);
            CheckReferences("ForeignSceneReference", report.ForeignSceneReferences, budget, observed, problems);
            CheckReferences("UnresolvedReference", report.Unresolved, budget, observed, problems);
            foreach (KnownException exception in budget.knownExceptions)
            {
                string key = exception.rule + "|" + exception.source + "|" + exception.objectId + "|" + exception.target;
                if (exception.rule == "ExternalPackageGuid" || exception.rule == "ExternalPackageInclude")
                    key = exception.rule + "|" + exception.target;
                if (string.IsNullOrWhiteSpace(exception.reason) || !observed.Contains(key))
                    problems.Add("DemoContent.StaleKnownException: " + key);
            }
            return problems;
        }

        static void CheckReferences(string rule, IEnumerable<Reference> references, Budget budget, HashSet<string> observed, List<string> problems)
        {
            foreach (Reference reference in references)
            {
                string target = reference.Target ?? reference.Guid;
                string key = rule + "|" + reference.Source + "|" + reference.ObjectId + "|" + target;
                observed.Add(key);
                if (rule == "UnresolvedReference" && reference.Target == null &&
                    budget.knownExceptions.Any(e => e.rule == "ExternalPackageGuid" && e.target == reference.Guid))
                {
                    observed.Add("ExternalPackageGuid|" + reference.Guid);
                    continue;
                }
                if (rule == "UnresolvedReference" && reference.ObjectId == "include" &&
                    reference.Target != null && reference.Target.StartsWith("Packages/", StringComparison.Ordinal) &&
                    budget.knownExceptions.Any(e => e.rule == "ExternalPackageInclude" && e.target == reference.Target))
                {
                    observed.Add("ExternalPackageInclude|" + reference.Target);
                    continue;
                }
                if (!budget.knownExceptions.Any(e => e.rule == rule && e.source == reference.Source && e.objectId == reference.ObjectId && e.target == target))
                    problems.Add("DemoContent." + rule + ": " + reference);
            }
        }

        public static string Inventory(Report report)
        {
            var text = new StringBuilder("set\tbytes\tstorage\tpath\troot_or_dependency_chain\n");
            foreach (Asset asset in report.Assets.Values)
            {
                bool reached = report.Parent.ContainsKey(asset.Path);
                text.Append(reached ? "reachable" : "unreachable").Append('\t').Append(asset.Bytes).Append('\t')
                    .Append(asset.Pointer ? "lfs-pointer-size" : "disk").Append('\t').Append(asset.Path).Append('\t');
                if (reached) text.Append(report.Chain(asset.Path));
                text.Append('\n');
            }
            return text.ToString();
        }
    }
}
